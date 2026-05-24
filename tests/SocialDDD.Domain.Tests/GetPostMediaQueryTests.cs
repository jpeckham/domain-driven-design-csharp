using FluentAssertions;
using SocialDDD.Application.Social.Posts.Queries;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Tests;

public class GetPostMediaQueryTests
{
    [Fact]
    public async Task HandleAsync_LoadsPersistedStorageKeyAndContentType()
    {
        var assetId = Guid.NewGuid();
        var storageKey = $"stored/{assetId}";
        var bytes = new byte[] { 1, 2, 3 };
        var media = new PostMedia(
            assetId,
            MediaKind.Image,
            storageKey,
            "image/png",
            bytes.Length,
            10,
            10,
            null,
            null,
            null,
            0);
        var post = Post.Create(UserId.New(), null, [media]);
        var storage = new CapturingPostMediaStorage(bytes);
        var handler = new GetPostMediaQueryHandler(new SinglePostRepository(post), storage);

        var result = await handler.HandleAsync(new GetPostMediaQuery(assetId));

        result.ContentType.Should().Be("image/png");
        storage.LoadedStorageKey.Should().Be(storageKey);
        using var ms = new MemoryStream();
        await result.Stream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(bytes);
    }

    private sealed class CapturingPostMediaStorage(byte[] bytes) : IPostMediaStorageService
    {
        public string? LoadedStorageKey { get; private set; }

        public Task<(string uploadUrl, string storageKey)> ReserveUploadAsync(Guid assetId, string contentType, CancellationToken ct) =>
            Task.FromResult(("", ""));

        public Task StoreAsync(Guid assetId, string storageKey, Stream data, string contentType, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<Stream> LoadAsync(string storageKey, CancellationToken ct)
        {
            LoadedStorageKey = storageKey;
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public Task<string?> GetContentTypeAsync(string storageKey, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task DeleteAsync(string storageKey, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SinglePostRepository(Post post) : IPostRepository
    {
        public Task AddAsync(Post post, CancellationToken ct = default) => Task.CompletedTask;
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
        public Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default) =>
            Task.FromResult(post.Media.Any(m => m.AssetId == assetId) ? post : null);
        public Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Post>> SearchAsync(string query, Handle? requesterHandle, IReadOnlySet<Handle> excludedHandles, int limit, int offset, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Post>>([]);
    }
}
