using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Identity.Accounts.Commands;
using SocialDDD.Application.Social.Profiles.Queries;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;
using SocialDDD.Domain.Identity.Users.Events;

namespace SocialDDD.Domain.Tests;

// ── Domain unit tests ────────────────────────────────────────────────────────

public class ProfileImageDomainTests
{
    private static User MakeActiveUser() =>
        User.RegisterImmediate(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash"),
            new Handle("alice"),
            new DisplayName("Alice"));

    private static ProfileImage MakeImage(Guid? assetId = null) =>
        new(
            assetId ?? Guid.NewGuid(),
            "profile-images/test",
            "image/jpeg",
            12345,
            Width: 400,
            Height: 400,
            DateTimeOffset.UtcNow);

    [Fact]
    public void SetProfileImage_RaisesProfileImageUpdatedEvent()
    {
        var user = MakeActiveUser();
        user.PopDomainEvents();
        var image = MakeImage();

        user.SetProfileImage(image);

        user.ProfileImage.Should().Be(image);
        var events = user.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<ProfileImageUpdated>()
            .Which.AssetId.Should().Be(image.AssetId);
    }

    [Fact]
    public void RemoveProfileImage_WhenNoImage_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        user.PopDomainEvents();

        var act = () => user.RemoveProfileImage();

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void RemoveProfileImage_WhenImageSet_RaisesProfileImageRemovedEvent()
    {
        var user = MakeActiveUser();
        user.SetProfileImage(MakeImage());
        user.PopDomainEvents();

        user.RemoveProfileImage();

        user.ProfileImage.Should().BeNull();
        var events = user.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<ProfileImageRemoved>()
            .Which.UserId.Should().Be(user.Id);
    }
}

// ── Application command tests (fakes, no infrastructure) ────────────────────

public class ProfileImageCommandTests
{
    private static User MakeUser(string handle = "alice") =>
        User.RegisterImmediate(
            new Username(handle),
            new Email($"{handle}@example.com"),
            new PasswordHash("hash"),
            new Handle(handle),
            new DisplayName("Alice"));

    [Fact]
    public async Task BeginUpload_InvalidContentType_ThrowsDomainValidationException()
    {
        var user = MakeUser();
        var handler = new BeginProfileImageUploadCommandHandler(
            new FakeUserRepo(user),
            new FakeStorageService());

        var act = () => handler.HandleAsync(new BeginProfileImageUploadCommand(user.Id.Value, "image/gif"));

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task BeginUpload_UserNotFound_ThrowsDomainException()
    {
        var handler = new BeginProfileImageUploadCommandHandler(
            new FakeUserRepo(null),
            new FakeStorageService());

        var act = () => handler.HandleAsync(new BeginProfileImageUploadCommand(Guid.NewGuid(), "image/jpeg"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task BeginUpload_ValidRequest_ReturnsAssetIdAndUploadUrl()
    {
        var user = MakeUser();
        var handler = new BeginProfileImageUploadCommandHandler(
            new FakeUserRepo(user),
            new FakeStorageService());

        var (assetId, uploadUrl) = await handler.HandleAsync(new BeginProfileImageUploadCommand(user.Id.Value, "image/png"));

        assetId.Should().NotBeEmpty();
        uploadUrl.Should().Contain(assetId.ToString());
    }

    [Fact]
    public async Task CompleteUpload_SetsProfileImageOnUser()
    {
        var user = MakeUser();
        var repo = new FakeUserRepo(user);
        var handler = new CompleteProfileImageUploadCommandHandler(
            repo,
            new FakeStorageService(),
            new FakeDispatcher());

        await handler.HandleAsync(new CompleteProfileImageUploadCommand(
            user.Id.Value, Guid.NewGuid(), "image/jpeg", 5000, 200, 200));

        user.ProfileImage.Should().NotBeNull();
        user.ProfileImage!.ContentType.Should().Be("image/jpeg");
        repo.Updated.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteUpload_WhenUploadMissing_ThrowsDomainException()
    {
        var user = MakeUser();
        var repo = new FakeUserRepo(user);
        var handler = new CompleteProfileImageUploadCommandHandler(
            repo,
            new FakeStorageService { Exists = false },
            new FakeDispatcher());

        var act = async () => await handler.HandleAsync(new CompleteProfileImageUploadCommand(
            user.Id.Value, Guid.NewGuid(), "image/jpeg", 5000, 200, 200));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Uploaded profile image was not found.");
        repo.Updated.Should().BeFalse();
        user.ProfileImage.Should().BeNull();
    }


    [Fact]
    public async Task CompleteUpload_WithExistingImage_DeletesOldStorageKey()
    {
        var user = MakeUser();
        var existingImage = new ProfileImage(Guid.NewGuid(), "old-key", "image/jpeg", 100, null, null, DateTimeOffset.UtcNow);
        user.SetProfileImage(existingImage);
        user.PopDomainEvents();

        var storage = new FakeStorageService();
        var handler = new CompleteProfileImageUploadCommandHandler(
            new FakeUserRepo(user),
            storage,
            new FakeDispatcher());

        await handler.HandleAsync(new CompleteProfileImageUploadCommand(
            user.Id.Value, Guid.NewGuid(), "image/png", 8000, null, null));

        storage.DeletedKeys.Should().Contain("old-key");
    }

    [Fact]
    public async Task RemoveProfileImage_WhenImageExists_RemovesAndSaves()
    {
        var user = MakeUser();
        user.SetProfileImage(new ProfileImage(Guid.NewGuid(), "key-1", "image/jpeg", 100, null, null, DateTimeOffset.UtcNow));
        user.PopDomainEvents();

        var storage = new FakeStorageService();
        var repo = new FakeUserRepo(user);
        var handler = new RemoveProfileImageCommandHandler(repo, storage, new FakeDispatcher());

        await handler.HandleAsync(new RemoveProfileImageCommand(user.Id.Value));

        user.ProfileImage.Should().BeNull();
        storage.DeletedKeys.Should().Contain("key-1");
        repo.Updated.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveProfileImage_WhenNoImage_ThrowsDomainValidationException()
    {
        var user = MakeUser();
        var handler = new RemoveProfileImageCommandHandler(
            new FakeUserRepo(user),
            new FakeStorageService(),
            new FakeDispatcher());

        var act = () => handler.HandleAsync(new RemoveProfileImageCommand(user.Id.Value));

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task GetProfileImage_ReturnsStreamAndContentType()
    {
        var assetId = Guid.NewGuid();
        var user = MakeUser();
        user.SetProfileImage(new ProfileImage(assetId, "key-1", "image/jpeg", 100, null, null, DateTimeOffset.UtcNow));
        user.PopDomainEvents();

        var storage = new FakeStorageService();
        var handler = new GetProfileImageQueryHandler(new FakeUserRepo(user), storage);

        var (stream, contentType) = await handler.HandleAsync(new GetProfileImageQuery(assetId));

        stream.Should().NotBeNull();
        contentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task GetProfileImage_AssetNotFound_ThrowsDomainException()
    {
        var handler = new GetProfileImageQueryHandler(
            new FakeUserRepo(null),
            new FakeStorageService());

        Func<Task> act = async () => await handler.HandleAsync(new GetProfileImageQuery(Guid.NewGuid()));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetProfileImage_WhenStoredFileIsMissing_ThrowsDomainException()
    {
        var assetId = Guid.NewGuid();
        var user = MakeUser();
        user.SetProfileImage(new ProfileImage(assetId, "missing-key", "image/jpeg", 100, null, null, DateTimeOffset.UtcNow));
        user.PopDomainEvents();

        var handler = new GetProfileImageQueryHandler(
            new FakeUserRepo(user),
            new FakeStorageService { MissingOnLoad = true });

        Func<Task> act = async () => await handler.HandleAsync(new GetProfileImageQuery(assetId));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage($"Profile image {assetId} file is missing.");
    }

    // ── fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeUserRepo(User? user) : IUserRepository
    {
        private User? _user = user;
        public bool Updated { get; private set; }

        public Task AddAsync(User u, CancellationToken ct = default) { _user = u; return Task.CompletedTask; }
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(_user?.Id == id ? _user : null);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(_user?.Email == email ? _user : null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(_user?.Username == username ? _user : null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(_user?.Handle == handle ? _user : null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(_user?.Email == email);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(_user?.Username == username);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(_user?.Id == id);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(_user?.Handle == handle);
        public Task UpdateAsync(User u, CancellationToken ct = default) { _user = u; Updated = true; return Task.CompletedTask; }
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default)
            => Task.FromResult(_user?.ProfileImage?.AssetId == assetId ? _user : null);
    }

    private sealed class FakeStorageService : IProfileImageStorageService
    {
        public List<string> DeletedKeys { get; } = [];
        public bool Exists { get; init; } = true;
        public bool MissingOnLoad { get; init; }

        public Task<(string uploadUrl, string storageKey)> ReserveUploadAsync(Guid assetId, string contentType, CancellationToken ct)
            => Task.FromResult(($"/api/media/uploads/profile/{assetId}", assetId.ToString()));

        public Task StoreAsync(Guid assetId, string storageKey, Stream data, string contentType, CancellationToken ct)
            => Task.CompletedTask;

        public Task<bool> ExistsAsync(string storageKey, CancellationToken ct) => Task.FromResult(Exists);

        public Task<Stream> LoadAsync(string storageKey, CancellationToken ct)
            => MissingOnLoad
                ? throw new FileNotFoundException("missing")
                : Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));

        public Task DeleteAsync(string storageKey, CancellationToken ct)
        {
            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
