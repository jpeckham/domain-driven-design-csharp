using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts;

public interface IPostRepository
{
    Task AddAsync(Post post, CancellationToken ct = default);
    Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, int limit, int offset, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetFeedAsync(
        int skip,
        int limit,
        bool rootOnly = false,
        IReadOnlySet<Handle>? excludedHandles = null,
        IReadOnlySet<Handle>? includedHandles = null,
        CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default);
    Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default);
    Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetConversationAsync(PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct = default);
    Task<int> CountRepliesAsync(PostId parentPostId, CancellationToken ct = default);
    Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default);
    Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default);
    Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> SearchAsync(
        string query,
        Handle? requesterHandle,
        IReadOnlySet<Handle> excludedHandles,
        int limit,
        int offset,
        CancellationToken ct = default);
}
