using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Social.Posts;
using SocialDDD.Application.Social.Posts.DTOs;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Tests;

public class PostMediaOnlyApplicationTests
{
    [Fact]
    public async Task CreateAsync_WithMediaAndBlankContent_CreatesMediaOnlyPost()
    {
        var author = User.RegisterImmediate(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        var assetId = Guid.NewGuid();
        var media = new PostMedia(
            assetId, MediaKind.Image, "key", "image/jpeg", 1024, 800, 600, null, null, null, 0);
        var postRepository = new CapturingPostRepository();
        var service = new PostService(
            postRepository,
            new SingleUserRepository(author),
            new EmptyBlockRepository(),
            new EmptyFollowRepository(),
            new NoOpDispatcher(),
            new SinglePendingMediaStore(media));

        var result = await service.CreateAsync(new CreatePostRequest(author.Id.Value, "   ", [assetId]));

        result.Content.Should().BeNull();
        result.Media.Should().ContainSingle(m => m.AssetId == assetId);
        postRepository.AddedPost.Should().NotBeNull();
        postRepository.AddedPost!.Content.Should().BeNull();
    }

    private sealed class CapturingPostRepository : IPostRepository
    {
        public Post? AddedPost { get; private set; }
        public Task AddAsync(Post post, CancellationToken ct = default)
        {
            AddedPost = post;
            return Task.CompletedTask;
        }

        public Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default) => Task.FromResult<Post?>(null);
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
        public Task IncrementReplyCountsAsync(IReadOnlyList<PostId> postIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default) => Task.FromResult<Post?>(null);
        public Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<Post?>(null);
        public Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Post>> SearchAsync(string query, Handle? requesterHandle, IReadOnlySet<Handle> excludedHandles, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
    }

    private sealed class SingleUserRepository(User user) : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user.Id == id ? user : null);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user.Handle == handle ? user : null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user.Id == id);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    }

    private sealed class SinglePendingMediaStore(PostMedia media) : IPendingMediaStore
    {
        public void Reserve(Guid assetId) { }
        public bool IsReserved(Guid assetId) => false;
        public void Complete(Guid assetId, PostMedia media) { }
        public bool TryGetCompleted(Guid assetId, out PostMedia? result)
        {
            result = assetId == media.AssetId ? media : null;
            return result is not null;
        }
    }

    private sealed class EmptyBlockRepository : IBlockRepository
    {
        public Task SaveAsync(Block block, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Block block, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Block?> FindAsync(Handle blocker, Handle blocked, CancellationToken ct = default) => Task.FromResult<Block?>(null);
        public Task<IReadOnlyList<Handle>> GetBlockedHandlesAsync(Handle blocker, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Handle>>([]);
        public Task<IReadOnlyList<Handle>> GetBlockerHandlesAsync(Handle blocked, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Handle>>([]);
        public Task<bool> IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class EmptyFollowRepository : IFollowRepository
    {
        public Task SaveAsync(Follow follow, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Follow follow, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default) => Task.FromResult<Follow?>(null);
        public Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Handle>>([]);
        public Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default) => Task.CompletedTask;
    }
}
