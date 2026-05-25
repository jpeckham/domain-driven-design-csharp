using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Social.Posts;
using SocialDDD.Application.Identity.Accounts;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Social.Follows;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Tests;

public class ProfilePageApplicationTests
{
    [Fact]
    public async Task GetProfileByHandleAsync_ReturnsCountsImageAndRelationshipFlags()
    {
        var viewer = User.RegisterImmediate(
            new Username("alice"), new Email("alice@example.com"), new PasswordHash("hash"),
            new Handle("alice"), new DisplayName("Alice"));
        var target = User.RegisterImmediate(
            new Username("bob"), new Email("bob@example.com"), new PasswordHash("hash"),
            new Handle("bob"), new DisplayName("Bob"));
        target.SetProfileImage(new ProfileImage(Guid.NewGuid(), "key", "image/png", 10, 1, 1, DateTimeOffset.UtcNow));

        var follows = new FakeFollowRepository();
        await follows.SaveAsync(Follow.Create(viewer.Handle, target.Handle));
        await follows.SaveAsync(Follow.Create(target.Handle, viewer.Handle));
        var blocks = new FakeBlockRepository([Block.Create(viewer.Handle, target.Handle)]);
        var service = new UserService(
            new FakeUserRepository([viewer, target]),
            new FakePasswordHasher(),
            new FakeTokenService(),
            new NoOpDomainEventDispatcher(),
            follows,
            blocks);

        var profile = await service.GetProfileByHandleAsync("bob", viewer.Id.Value);

        profile.Handle.Should().Be("@bob");
        profile.ProfileImageUrl.Should().Be($"/api/profile-images/{target.ProfileImage.AssetId}");
        profile.FollowerCount.Should().Be(1);
        profile.FollowingCount.Should().Be(1);
        profile.IsFollowedByMe.Should().BeTrue();
        profile.IsBlockedByMe.Should().BeTrue();
        profile.IsOwnProfile.Should().BeFalse();
    }

    [Fact]
    public async Task GetByAuthorHandleAsync_ReturnsPagedPostsWithAuthorDisplayData()
    {
        var author = User.RegisterImmediate(
            new Username("bob"), new Email("bob@example.com"), new PasswordHash("hash"),
            new Handle("bob"), new DisplayName("Bob Builder"));
        var oldPost = Post.Create(author.Id, new PostContent("old"));
        var newPost = Post.Create(author.Id, new PostContent("new"));
        var repository = new FakePostRepository([oldPost, newPost]);
        var service = new PostService(
            repository,
            new FakeUserRepository([author]),
            new FakeBlockRepository(),
            new FakeFollowRepository(),
            new NoOpDomainEventDispatcher(),
            new FakePendingMediaStore());

        var posts = await service.GetByAuthorHandleAsync("bob", limit: 1, offset: 0);

        posts.Should().ContainSingle();
        posts[0].Content.Should().Be("new");
        posts[0].AuthorDisplayName.Should().Be("Bob Builder");
        posts[0].AuthorHandle.Should().Be("@bob");
        repository.LastLimit.Should().Be(1);
        repository.LastOffset.Should().Be(0);
    }

    [Fact]
    public async Task GetFeedAsync_ExcludesBlockedUsersBeforeRepositoryPagination()
    {
        var viewer = User.RegisterImmediate(
            new Username("alice"), new Email("alice@example.com"), new PasswordHash("hash"),
            new Handle("alice"), new DisplayName("Alice"));
        var blocked = User.RegisterImmediate(
            new Username("bob"), new Email("bob@example.com"), new PasswordHash("hash"),
            new Handle("bob"), new DisplayName("Bob"));
        var visible = User.RegisterImmediate(
            new Username("cara"), new Email("cara@example.com"), new PasswordHash("hash"),
            new Handle("cara"), new DisplayName("Cara"));
        var blockedPost = Post.Create(blocked.Id, new PostContent("hidden"));
        var visiblePost = Post.Create(visible.Id, new PostContent("visible"));
        var repository = new FakePostRepository(
            [blockedPost, visiblePost],
            new Dictionary<UserId, Handle>
            {
                [blocked.Id] = blocked.Handle,
                [visible.Id] = visible.Handle
            });
        var service = new PostService(
            repository,
            new FakeUserRepository([viewer, blocked, visible]),
            new FakeBlockRepository([Block.Create(viewer.Handle, blocked.Handle)]),
            new FakeFollowRepository(),
            new NoOpDomainEventDispatcher(),
            new FakePendingMediaStore());

        var posts = await service.GetFeedAsync(skip: 0, limit: 1, requesterId: viewer.Id.Value);

        posts.Should().ContainSingle();
        posts[0].AuthorHandle.Should().Be("@cara");
        repository.LastExcludedHandles.Should().Contain(new Handle("bob"));
        repository.LastRootOnly.Should().BeTrue();
    }

    [Fact]
    public async Task GetByAuthorAsync_WhenRequesterBlockedAuthor_ReturnsNoPosts()
    {
        var viewer = User.RegisterImmediate(
            new Username("alice"), new Email("alice@example.com"), new PasswordHash("hash"),
            new Handle("alice"), new DisplayName("Alice"));
        var blocked = User.RegisterImmediate(
            new Username("bob"), new Email("bob@example.com"), new PasswordHash("hash"),
            new Handle("bob"), new DisplayName("Bob"));
        var blockedPost = Post.Create(blocked.Id, new PostContent("hidden"));
        var repository = new FakePostRepository([blockedPost]);
        var service = new PostService(
            repository,
            new FakeUserRepository([viewer, blocked]),
            new FakeBlockRepository([Block.Create(viewer.Handle, blocked.Handle)]),
            new FakeFollowRepository(),
            new NoOpDomainEventDispatcher(),
            new FakePendingMediaStore());

        var posts = await service.GetByAuthorAsync(blocked.Id.Value, viewer.Id.Value);

        posts.Should().BeEmpty();
    }

    private sealed class FakeUserRepository(IReadOnlyList<User> users) : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
            Task.FromResult(users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) =>
            Task.FromResult(users.FirstOrDefault(u => u.Handle == handle));
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) =>
            Task.FromResult(users.Any(u => u.Id == id));
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    }

    private sealed class FakePostRepository(
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<UserId, Handle>? authorHandles = null) : IPostRepository
    {
        public int? LastLimit { get; private set; }
        public int? LastOffset { get; private set; }
        public bool? LastRootOnly { get; private set; }

        public Task AddAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default) => Task.FromResult(posts.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>(posts.Where(p => p.AuthorId == authorId && !p.IsDeleted).OrderByDescending(p => p.PostedAt).ToList());
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, int limit, int offset, CancellationToken ct = default)
        {
            LastLimit = limit;
            LastOffset = offset;
            return Task.FromResult<IReadOnlyList<Post>>(posts
                .Where(p => p.AuthorId == authorId && !p.IsDeleted)
                .OrderByDescending(p => p.PostedAt)
                .Skip(offset)
                .Take(limit)
                .ToList());
        }
        public IReadOnlySet<Handle> LastExcludedHandles { get; private set; } = new HashSet<Handle>();
        public Task<IReadOnlyList<Post>> GetFeedAsync(
            int skip,
            int limit,
            bool rootOnly = false,
            IReadOnlySet<Handle>? excludedHandles = null,
            IReadOnlySet<Handle>? includedHandles = null,
            CancellationToken ct = default)
        {
            LastRootOnly = rootOnly;
            LastExcludedHandles = excludedHandles ?? new HashSet<Handle>();
            var excluded = LastExcludedHandles;
            return Task.FromResult<IReadOnlyList<Post>>(posts
                .Where(p => !p.IsDeleted)
                .Where(p => authorHandles is null
                    || !authorHandles.TryGetValue(p.AuthorId, out var handle)
                    || !excluded.Contains(handle))
                .OrderByDescending(p => p.PostedAt)
                .Skip(skip)
                .Take(limit)
                .ToList());
        }
        public Task UpdateAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Post>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetConversationAsync(PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<int> CountRepliesAsync(PostId parentPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task IncrementReplyCountsAsync(IReadOnlyList<PostId> postIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default) => Task.FromResult<Post?>(null);
        public Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default) =>
            Task.FromResult(posts.FirstOrDefault(p => p.Media.Any(m => m.AssetId == assetId) && !p.IsDeleted));
        public Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Post>> SearchAsync(string query, Handle? requesterHandle, IReadOnlySet<Handle> excludedHandles, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
    }

    private sealed class FakeFollowRepository : IFollowRepository
    {
        private readonly List<Follow> _follows = [];
        public Task SaveAsync(Follow follow, CancellationToken ct = default) { _follows.Add(follow); return Task.CompletedTask; }
        public Task DeleteAsync(Follow follow, CancellationToken ct = default) { _follows.Remove(follow); return Task.CompletedTask; }
        public Task<Follow?> FindAsync(Handle follower, Handle followed, CancellationToken ct = default) =>
            Task.FromResult(_follows.FirstOrDefault(f => f.FollowerHandle == follower && f.FollowedHandle == followed));
        public Task<bool> IsFollowingAsync(Handle follower, Handle followed, CancellationToken ct = default) =>
            Task.FromResult(_follows.Any(f => f.FollowerHandle == follower && f.FollowedHandle == followed));
        public Task<IReadOnlyList<Handle>> GetFollowedHandlesAsync(Handle follower, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Handle>>(_follows.Where(f => f.FollowerHandle == follower).Select(f => f.FollowedHandle).ToList());
        public Task<int> CountFollowersAsync(Handle followed, CancellationToken ct = default) =>
            Task.FromResult(_follows.Count(f => f.FollowedHandle == followed));
        public Task<int> CountFollowingAsync(Handle follower, CancellationToken ct = default) =>
            Task.FromResult(_follows.Count(f => f.FollowerHandle == follower));
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

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash";
        public bool Verify(string password, string hash) => true;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateToken(User user) => "token";
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
