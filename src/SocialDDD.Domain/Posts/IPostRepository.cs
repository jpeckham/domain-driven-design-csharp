using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts;

public interface IPostRepository
{
    Task AddAsync(Post post, CancellationToken ct = default);
    Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetFeedAsync(int skip, int limit, CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default);
    Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default);
    Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default);
}
