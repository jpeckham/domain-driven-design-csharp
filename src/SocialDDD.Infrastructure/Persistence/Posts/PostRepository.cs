using MongoDB.Driver;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Posts;

internal sealed class PostRepository(MongoDbContext context) : IPostRepository
{
    public Task AddAsync(Post post, CancellationToken ct = default) =>
        context.Posts.InsertOneAsync(post, cancellationToken: ct);

    public async Task<Post?> GetByIdAsync(PostId id, CancellationToken ct = default) =>
        await context.Posts
            .Find(p => p.Id == id && !p.IsDeleted)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Post>> GetByAuthorAsync(UserId authorId, CancellationToken ct = default)
    {
        var results = await context.Posts
            .Find(p => p.AuthorId == authorId && !p.IsDeleted)
            .SortByDescending(p => p.PostedAt)
            .ToListAsync(ct);
        return results;
    }

    public async Task<IReadOnlyList<Post>> GetFeedAsync(int skip, int limit, CancellationToken ct = default)
    {
        var results = await context.Posts
            .Find(p => !p.IsDeleted)
            .SortByDescending(p => p.PostedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);
        return results;
    }

    public Task UpdateAsync(Post post, CancellationToken ct = default) =>
        context.Posts.ReplaceOneAsync(
            p => p.Id == post.Id,
            post,
            cancellationToken: ct);

    public Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.Eq(p => p.Id, postId);
        var update = Builders<Post>.Update.AddToSet("likedBy", handle.Value);
        return context.Posts.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.Eq(p => p.Id, postId);
        var update = Builders<Post>.Update.Pull("likedBy", handle.Value);
        return context.Posts.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.Eq(p => p.Id, postId),
            Builders<Post>.Filter.AnyEq("likedBy", handle.Value));
        return await context.Posts.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }
}
