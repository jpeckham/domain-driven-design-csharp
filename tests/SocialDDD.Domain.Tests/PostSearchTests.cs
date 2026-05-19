using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts;
using SocialDDD.Application.Posts.Queries;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Follows;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class PostSearchTests
{
    [Fact]
    public async Task SearchPostsQueryHandler_EmptyQuery_ThrowsDomainValidationException()
    {
        var repository = new FakePostRepository([]);
        var handler = new SearchPostsQueryHandler(
            new PostService(repository, new FakeUserRepository(), new FakeBlockRepository(), new FakeFollowRepository(), new NoOpDomainEventDispatcher(), new FakePendingMediaStore()));

        var act = async () => await handler.HandleAsync(new SearchPostsQuery(" "));

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Search query is required.");
        repository.SearchWasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task SearchPostsQueryHandler_SearchResults_ExcludeDeletedPosts()
    {
        var visible = Post.Create(UserId.New(), new PostContent("hello visible"));
        var deleted = Post.Create(UserId.New(), new PostContent("hello deleted"));
        deleted.Delete();
        var repository = new FakePostRepository([visible, deleted]);
        var handler = new SearchPostsQueryHandler(
            new PostService(repository, new FakeUserRepository(), new FakeBlockRepository(), new FakeFollowRepository(), new NoOpDomainEventDispatcher(), new FakePendingMediaStore()));

        var result = await handler.HandleAsync(new SearchPostsQuery("hello", Limit: 10, Offset: 0));

        result.Query.Should().Be("hello");
        result.Limit.Should().Be(10);
        result.Offset.Should().Be(0);
        result.Posts.Should().ContainSingle();
        result.Posts[0].PostId.Should().Be(visible.Id.Value);
    }

    [Fact]
    public async Task SearchPostsQueryHandler_SearchResults_ExcludeAuthorsBlockedByRequester()
    {
        var requester = new Handle("alice");
        var blockedAuthor = User.RegisterImmediate(
            new Username("bob"),
            new Email("bob@example.com"),
            new PasswordHash("hash"),
            new Handle("bob"),
            new DisplayName("Bob"));
        var visibleAuthor = User.RegisterImmediate(
            new Username("charlie"),
            new Email("charlie@example.com"),
            new PasswordHash("hash"),
            new Handle("charlie"),
            new DisplayName("Charlie"));
        var blockedPost = Post.Create(blockedAuthor.Id, new PostContent("hello blocked"));
        var visiblePost = Post.Create(visibleAuthor.Id, new PostContent("hello visible"));

        var handler = new SearchPostsQueryHandler(
            new PostService(
                new FakePostRepository([blockedPost, visiblePost], [blockedAuthor, visibleAuthor]),
                new FakeUserRepository([blockedAuthor, visibleAuthor]),
                new FakeBlockRepository([Block.Create(requester, blockedAuthor.Handle)]),
                new FakeFollowRepository(),
                new NoOpDomainEventDispatcher(),
                new FakePendingMediaStore()));

        var result = await handler.HandleAsync(new SearchPostsQuery("hello", requester, Limit: 10));

        result.Posts.Should().ContainSingle();
        result.Posts[0].AuthorId.Should().Be(visibleAuthor.Id.Value);
    }

    [Fact]
    public async Task SearchPostsQueryHandler_SearchResults_ExcludeAuthorsWhoBlockedRequester()
    {
        var requester = new Handle("alice");
        var blockingAuthor = User.RegisterImmediate(
            new Username("dana"),
            new Email("dana@example.com"),
            new PasswordHash("hash"),
            new Handle("dana"),
            new DisplayName("Dana"));
        var visibleAuthor = User.RegisterImmediate(
            new Username("erin"),
            new Email("erin@example.com"),
            new PasswordHash("hash"),
            new Handle("erin"),
            new DisplayName("Erin"));
        var blockedPost = Post.Create(blockingAuthor.Id, new PostContent("hello blocked"));
        var visiblePost = Post.Create(visibleAuthor.Id, new PostContent("hello visible"));

        var handler = new SearchPostsQueryHandler(
            new PostService(
                new FakePostRepository([blockedPost, visiblePost], [blockingAuthor, visibleAuthor]),
                new FakeUserRepository([blockingAuthor, visibleAuthor]),
                new FakeBlockRepository([Block.Create(blockingAuthor.Handle, requester)]),
                new FakeFollowRepository(),
                new NoOpDomainEventDispatcher(),
                new FakePendingMediaStore()));

        var result = await handler.HandleAsync(new SearchPostsQuery("hello", requester, Limit: 10));

        result.Posts.Should().ContainSingle();
        result.Posts[0].AuthorId.Should().Be(visibleAuthor.Id.Value);
    }

    [Fact]
    public async Task SearchPostsQueryHandler_BlockedFirstMatch_DoesNotConsumePageLimit()
    {
        var requester = new Handle("alice");
        var blockedAuthor = User.RegisterImmediate(
            new Username("frank"),
            new Email("frank@example.com"),
            new PasswordHash("hash"),
            new Handle("frank"),
            new DisplayName("Frank"));
        var visibleAuthor = User.RegisterImmediate(
            new Username("grace"),
            new Email("grace@example.com"),
            new PasswordHash("hash"),
            new Handle("grace"),
            new DisplayName("Grace"));
        var blockedPost = Post.Create(blockedAuthor.Id, new PostContent("hello blocked first"));
        var visiblePost = Post.Create(visibleAuthor.Id, new PostContent("hello visible second"));

        var handler = new SearchPostsQueryHandler(
            new PostService(
                new FakePostRepository([blockedPost, visiblePost], [blockedAuthor, visibleAuthor]),
                new FakeUserRepository([blockedAuthor, visibleAuthor]),
                new FakeBlockRepository([Block.Create(requester, blockedAuthor.Handle)]),
                new FakeFollowRepository(),
                new NoOpDomainEventDispatcher(),
                new FakePendingMediaStore()));

        var result = await handler.HandleAsync(new SearchPostsQuery("hello", requester, Limit: 1, Offset: 0));

        result.Posts.Should().ContainSingle();
        result.Posts[0].AuthorId.Should().Be(visibleAuthor.Id.Value);
    }

    [Fact]
    public async Task SearchPostsQueryHandler_ExcessiveLimit_IsClampedToMaximum()
    {
        var repository = new FakePostRepository([]);
        var handler = new SearchPostsQueryHandler(
            new PostService(repository, new FakeUserRepository(), new FakeBlockRepository(), new FakeFollowRepository(), new NoOpDomainEventDispatcher(), new FakePendingMediaStore()));

        var result = await handler.HandleAsync(new SearchPostsQuery("hello", Limit: 1_000, Offset: -10));

        result.Limit.Should().Be(100);
        result.Offset.Should().Be(0);
        repository.LastLimit.Should().Be(100);
        repository.LastOffset.Should().Be(0);
    }

    private sealed class FakePostRepository(IReadOnlyList<Post> posts, IReadOnlyList<User>? users = null) : IPostRepository
    {
        private readonly IReadOnlyList<User> _users = users ?? [];
        public bool SearchWasCalled { get; private set; }
        public int? LastLimit { get; private set; }
        public int? LastOffset { get; private set; }

        public Task AddAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default) =>
            Task.FromResult(posts.FirstOrDefault(p => p.Id == id && !p.IsDeleted));
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>(posts.Where(p => p.AuthorId == authorId && !p.IsDeleted).ToList());
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, int limit, int offset, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>(posts.Where(p => p.AuthorId == authorId && !p.IsDeleted).Skip(offset).Take(limit).ToList());
        public Task<IReadOnlyList<Post>> GetFeedAsync(
            int skip,
            int limit,
            bool rootOnly = false,
            IReadOnlySet<Handle>? excludedHandles = null,
            IReadOnlySet<Handle>? includedHandles = null,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>(posts.Where(p => !p.IsDeleted).Skip(skip).Take(limit).ToList());
        public Task UpdateAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Post>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetConversationAsync(PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<int> CountRepliesAsync(PostId parentPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default) =>
            Task.FromResult<Post?>(null);
        public Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default) =>
            Task.FromResult(posts.FirstOrDefault(p => p.Media.Any(m => m.AssetId == assetId) && !p.IsDeleted));
        public Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Post>> SearchAsync(string query, Handle? requesterHandle, IReadOnlySet<Handle> excludedHandles, int limit, int offset, CancellationToken ct = default)
        {
            SearchWasCalled = true;
            LastLimit = limit;
            LastOffset = offset;
            var results = posts
                .Where(p => !p.IsDeleted && p.Content?.Value.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .Where(p => !_users.Any(u => u.Id == p.AuthorId && excludedHandles.Contains(u.Handle)))
                .OrderByDescending(p => p.PostedAt)
                .Skip(offset)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<Post>>(results);
        }
    }

    private sealed class FakeUserRepository(IReadOnlyList<User>? users = null) : IUserRepository
    {
        private readonly IReadOnlyList<User> _users = users ?? [];

        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Handle == handle));
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    }

    private sealed class FakeBlockRepository(IReadOnlyList<Block>? blocks = null) : IBlockRepository
    {
        private readonly IReadOnlyList<Block> _blocks = blocks ?? [];

        public Task SaveAsync(Block block, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Block block, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Block?> FindAsync(Handle blocker, Handle blocked, CancellationToken ct = default) =>
            Task.FromResult(_blocks.FirstOrDefault(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked));
        public Task<IReadOnlyList<Handle>> GetBlockedHandlesAsync(Handle blocker, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Handle>>(_blocks.Where(b => b.BlockerHandle == blocker).Select(b => b.BlockedHandle).ToList());
        public Task<IReadOnlyList<Handle>> GetBlockerHandlesAsync(Handle blocked, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Handle>>(_blocks.Where(b => b.BlockedHandle == blocked).Select(b => b.BlockerHandle).ToList());
        public Task<bool> IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct = default) =>
            Task.FromResult(_blocks.Any(b => b.BlockerHandle == blocker && b.BlockedHandle == blocked));
    }

    private sealed class FakeFollowRepository : IFollowRepository
    {
        public Task SaveAsync(Follow follow, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Follow follow, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default) => Task.FromResult<Follow?>(null);
        public Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Handle>>([]);
        public Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePendingMediaStore : IPendingMediaStore
    {
        public void Reserve(Guid assetId) { }
        public bool IsReserved(Guid assetId) => false;
        public void Complete(Guid assetId, PostMedia media) { }
        public bool TryGetCompleted(Guid assetId, out PostMedia? media)
        {
            media = null;
            return false;
        }
    }
}
