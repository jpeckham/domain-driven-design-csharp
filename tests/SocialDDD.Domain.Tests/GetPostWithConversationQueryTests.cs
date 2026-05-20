using FluentAssertions;
using SocialDDD.Application.Posts.Queries;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class GetPostWithConversationQueryTests
{
    [Fact]
    public async Task HandleAsync_IncludesParentPost_WhenFocusedPostIsReply()
    {
        var parentAuthor = MakeUser("alice");
        var replyAuthor = MakeUser("bob");
        var parent = Post.Create(parentAuthor.Id, new PostContent("Parent post"));
        var reply = Post.CreateReply(parent.Id, replyAuthor.Id, replyAuthor.Handle, new PostContent("Reply post"));
        var handler = new GetPostWithConversationQueryHandler(
            new FakePostRepository([parent, reply]),
            new FakeUserRepository([parentAuthor, replyAuthor]));

        var result = await handler.HandleAsync(
            new GetPostWithConversationQuery(reply.Id.Value),
            requesterHandle: null,
            requesterUserId: null);

        result.Post.PostId.Should().Be(reply.Id.Value);
        result.ParentPost.Should().NotBeNull();
        result.ParentPost!.PostId.Should().Be(parent.Id.Value);
        result.ParentPost.Content.Should().Be("Parent post");
    }

    private static User MakeUser(string handle) =>
        User.RegisterImmediate(
            new Username(handle),
            new Email($"{handle}@example.com"),
            new PasswordHash("hash"),
            new Handle(handle),
            new DisplayName(handle));

    private sealed class FakePostRepository(IReadOnlyList<Post> posts) : IPostRepository
    {
        public Task AddAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default) =>
            Task.FromResult(posts.FirstOrDefault(p => p.Id == id && !p.IsDeleted));
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetFeedAsync(int skip, int limit, bool rootOnly = false, IReadOnlySet<Handle>? excludedHandles = null, IReadOnlySet<Handle>? includedHandles = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task UpdateAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Post>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
        public Task<IReadOnlyList<Post>> GetConversationAsync(PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Post>>(posts.Where(p => p.ParentPostId == rootPostId && !p.IsDeleted).ToList());
        public Task<int> CountRepliesAsync(PostId parentPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task IncrementReplyCountsAsync(IReadOnlyList<PostId> postIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default) => Task.FromResult<Post?>(null);
        public Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<Post?>(null);
        public Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Post>> SearchAsync(string query, Handle? requesterHandle, IReadOnlySet<Handle> excludedHandles, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
    }

    private sealed class FakeUserRepository(IReadOnlyList<User> users) : IUserRepository
    {
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
            Task.FromResult(users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(users.FirstOrDefault(u => u.Handle == handle));
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
    }
}
