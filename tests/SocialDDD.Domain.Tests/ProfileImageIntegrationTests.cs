using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Identity.Accounts.Commands;
using SocialDDD.Application.Social.Profiles.Queries;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;
using SocialDDD.Infrastructure.Social.ProfileImages;

namespace SocialDDD.Domain.Tests;

public class ProfileImageIntegrationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"profile-images-test-{Guid.NewGuid()}");
    private readonly LocalFileProfileImageStorageService _storage;
    private readonly FakeUserRepo _repo;
    private readonly FakeDispatcher _dispatcher = new();
    private readonly User _user;

    public ProfileImageIntegrationTests()
    {
        Directory.CreateDirectory(_tempDir);
        _storage = new LocalFileProfileImageStorageService(_tempDir);
        _user = User.RegisterImmediate(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        _user.PopDomainEvents();
        _repo = new FakeUserRepo(_user);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task FullUploadFlow_BeginStoreThenCompleteThenServe()
    {
        var beginHandler = new BeginProfileImageUploadCommandHandler(_repo, _storage);
        var completeHandler = new CompleteProfileImageUploadCommandHandler(_repo, _storage, _dispatcher);
        var serveHandler = new GetProfileImageQueryHandler(_repo, _storage);

        // Begin upload — get assetId
        var (assetId, uploadUrl) = await beginHandler.HandleAsync(
            new BeginProfileImageUploadCommand(_user.Id.Value, "image/jpeg"));

        uploadUrl.Should().Contain(assetId.ToString());

        // Simulate PUT — write bytes directly via StoreAsync
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // minimal JPEG header
        using var uploadStream = new MemoryStream(imageBytes);
        await _storage.StoreAsync(assetId, assetId.ToString(), uploadStream, "image/jpeg", default);

        // Complete upload
        await completeHandler.HandleAsync(new CompleteProfileImageUploadCommand(
            _user.Id.Value, assetId, "image/jpeg", imageBytes.Length, 100, 100));

        _user.ProfileImage.Should().NotBeNull();
        _user.ProfileImage!.AssetId.Should().Be(assetId);

        // Serve image
        var (stream, contentType) = await serveHandler.HandleAsync(new GetProfileImageQuery(assetId));
        contentType.Should().Be("image/jpeg");
        using var ms = new MemoryStream();
        await using (stream)
            await stream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task DeleteProfileImage_RemovesFileFromDisk()
    {
        var assetId = Guid.NewGuid();
        var storageKey = assetId.ToString();
        var bytes = new byte[] { 1, 2, 3 };
        using var s = new MemoryStream(bytes);
        await _storage.StoreAsync(assetId, storageKey, s, "image/png", default);

        _user.SetProfileImage(new ProfileImage(assetId, storageKey, "image/png", bytes.Length, null, null, DateTimeOffset.UtcNow));
        _user.PopDomainEvents();

        var removeHandler = new RemoveProfileImageCommandHandler(_repo, _storage, _dispatcher);
        await removeHandler.HandleAsync(new RemoveProfileImageCommand(_user.Id.Value));

        _user.ProfileImage.Should().BeNull();
        var filePath = Path.Combine(_tempDir, storageKey);
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceProfileImage_DeletesOldFile()
    {
        var oldAssetId = Guid.NewGuid();
        var oldKey = oldAssetId.ToString();
        var bytes = new byte[] { 9, 8, 7 };
        using var oldStream = new MemoryStream(bytes);
        await _storage.StoreAsync(oldAssetId, oldKey, oldStream, "image/jpeg", default);

        _user.SetProfileImage(new ProfileImage(oldAssetId, oldKey, "image/jpeg", bytes.Length, null, null, DateTimeOffset.UtcNow));
        _user.PopDomainEvents();

        var newAssetId = Guid.NewGuid();
        using var newStream = new MemoryStream([1, 2, 3, 4]);
        await _storage.StoreAsync(newAssetId, newAssetId.ToString(), newStream, "image/png", default);

        var completeHandler = new CompleteProfileImageUploadCommandHandler(_repo, _storage, _dispatcher);
        await completeHandler.HandleAsync(new CompleteProfileImageUploadCommand(
            _user.Id.Value, newAssetId, "image/png", 4, null, null));

        _user.ProfileImage!.AssetId.Should().Be(newAssetId);
        File.Exists(Path.Combine(_tempDir, oldKey)).Should().BeFalse();
    }

    // ── fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeUserRepo(User user) : IUserRepository
    {
        private User _user = user;

        public Task AddAsync(User u, CancellationToken ct = default) { _user = u; return Task.CompletedTask; }
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult<User?>(_user.Id == id ? _user : null);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult<User?>(_user.Email == email ? _user : null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult<User?>(_user.Username == username ? _user : null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult<User?>(_user.Handle == handle ? _user : null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(_user.Email == email);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(_user.Username == username);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(_user.Id == id);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(_user.Handle == handle);
        public Task UpdateAsync(User u, CancellationToken ct = default) { _user = u; return Task.CompletedTask; }
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default)
            => Task.FromResult<User?>(_user.ProfileImage?.AssetId == assetId ? _user : null);
    }

    private sealed class FakeDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
