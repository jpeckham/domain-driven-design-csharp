using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.Commands;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class CreateRepostCommandTests
{
    [Fact]
    public async Task HandleAsync_ReturnsOriginalPostMediaForQuotedRepost()
    {
        var originalAuthor = MakeUser("alice");
        var reposter = MakeUser("bob");
        var assetId = Guid.NewGuid();
        var media = new PostMedia(
            assetId,
            MediaKind.Image,
            assetId.ToString(),
            "image/jpeg",
            1024,
            800,
            600,
            null,
            null,
            "Original image",
            0);
        var originalPost = Post.Create(originalAuthor.Id, new PostContent("with media"), [media]);
        originalPost.PopDomainEvents();
        var handler = new CreateRepostCommandHandler(
            new FakePostRepository(originalPost),
            new FakeUserRepository([originalAuthor, reposter]),
            new NoOpDispatcher());

        var result = await handler.HandleAsync(new CreateRepostCommand(
            originalPost.Id.Value,
            reposter.Id.Value,
            "look at this"));

        result.OriginalPost.Should().NotBeNull();
        result.OriginalPost!.Media.Should().ContainSingle(m => m.AssetId == assetId);
    }

    private static User MakeUser(string handle) =>
        User.RegisterImmediate(
            new Username(handle),
            new Email($"{handle}@example.com"),
            new PasswordHash("hash"),
            new Handle(handle),
            new DisplayName(handle));

    private sealed class FakePostRepository(Post originalPost) : IPostRepository
    {
        public Task AddAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default) =>
            Task.FromResult(id == originalPost.Id ? originalPost : null);
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetFeedAsync(int skip, int limit, bool rootOnly = false, IReadOnlySet<Handle>? excludedHandles = null, IReadOnlySet<Handle>? includedHandles = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task UpdateAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Post>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetConversationAsync(PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<int> CountRepliesAsync(PostId parentPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default) => Task.FromResult<Post?>(null);
        public Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult(originalPost.Media.Any(m => m.AssetId == assetId) ? originalPost : null);
        public Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Post>> SearchAsync(string query, Handle? requesterHandle, IReadOnlySet<Handle> excludedHandles, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
    }

    private sealed class FakeUserRepository(IReadOnlyList<User> users) : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(users.FirstOrDefault(u => u.Handle == handle));
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(users.Any(u => u.Id == id));
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(users.Any(u => u.Handle == handle));
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    }

    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default) => Task.CompletedTask;
    }
}
