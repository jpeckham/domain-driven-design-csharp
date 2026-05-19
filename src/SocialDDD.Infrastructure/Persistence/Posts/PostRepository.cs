using MongoDB.Bson;
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

    public async Task<IReadOnlyList<Post>> GetByAuthorAsync(
        UserId authorId, int limit, int offset, CancellationToken ct = default)
    {
        var results = await context.Posts
            .Find(p => p.AuthorId == authorId && !p.IsDeleted)
            .SortByDescending(p => p.PostedAt)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(ct);
        return results;
    }

    public async Task<IReadOnlyList<Post>> GetFeedAsync(
        int skip,
        int limit,
        bool rootOnly = false,
        IReadOnlySet<Handle>? excludedHandles = null,
        IReadOnlySet<Handle>? includedHandles = null,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<Post>>
        {
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false)
        };

        if (rootOnly)
            filters.Add(Builders<Post>.Filter.Eq("parentPostId", BsonNull.Value));

        if (excludedHandles is { Count: > 0 })
        {
            var excludedHandleValues = excludedHandles.Select(h => h.Value).ToList();
            var excludedAuthorIds = await context.Users
                .Find(Builders<User>.Filter.In("handle", excludedHandleValues))
                .Project(u => u.Id)
                .ToListAsync(ct);

            if (excludedAuthorIds.Count > 0)
                filters.Add(Builders<Post>.Filter.Nin(p => p.AuthorId, excludedAuthorIds));
        }

        if (includedHandles is { Count: > 0 })
        {
            var includedHandleValues = includedHandles.Select(h => h.Value).ToList();
            var includedAuthorIds = await context.Users
                .Find(Builders<User>.Filter.In("handle", includedHandleValues))
                .Project(u => u.Id)
                .ToListAsync(ct);

            filters.Add(includedAuthorIds.Count > 0
                ? Builders<Post>.Filter.In(p => p.AuthorId, includedAuthorIds)
                : Builders<Post>.Filter.Where(_ => false));
        }

        var filter = Builders<Post>.Filter.And(filters);

        var results = await context.Posts
            .Find(filter)
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

    public async Task<IReadOnlyList<Post>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.Eq("parentPostId", parentPostId.Value.ToString()),
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false));

        var results = await context.Posts
            .Find(filter)
            .SortByDescending(p => p.PostedAt)
            .Limit(limit)
            .ToListAsync(ct);
        return results;
    }

    public async Task<IReadOnlyList<Post>> GetConversationAsync(
        PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct = default)
    {
        var allDescendants = new List<Post>();
        var currentLevel = new List<PostId> { rootPostId };

        for (int depth = 0; depth < depthLimit && currentLevel.Count > 0; depth++)
        {
            var parentIds = currentLevel.Select(id => id.Value.ToString()).ToList();

            var filter = Builders<Post>.Filter.And(
                Builders<Post>.Filter.In("parentPostId", parentIds),
                Builders<Post>.Filter.Eq(p => p.IsDeleted, false));

            var levelPosts = await context.Posts
                .Find(filter)
                .SortByDescending(p => p.PostedAt)
                .Limit(repliesPerLevel * currentLevel.Count)
                .ToListAsync(ct);

            allDescendants.AddRange(levelPosts);
            currentLevel = levelPosts.Select(p => p.Id).ToList();
        }

        return allDescendants;
    }

    public async Task<int> CountRepliesAsync(PostId parentPostId, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.Eq("parentPostId", parentPostId.Value.ToString()),
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false));

        return (int)await context.Posts.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<Post?> FindRepostAsync(PostId originalPostId, UserId reposterUserId, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.Eq("originalPostId", originalPostId.Value.ToString()),
            Builders<Post>.Filter.Eq(p => p.AuthorId, reposterUserId),
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false));

        return await context.Posts.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Post?> FindByMediaAssetIdAsync(Guid assetId, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.ElemMatch(p => p.Media, m => m.AssetId == assetId),
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false));

        return await context.Posts.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct = default)
    {
        var filter = Builders<Post>.Filter.And(
            Builders<Post>.Filter.Eq("originalPostId", originalPostId.Value.ToString()),
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false));

        return (int)await context.Posts.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Post>> SearchAsync(
        string query,
        Handle? requesterHandle,
        IReadOnlySet<Handle> excludedHandles,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<Post>>
        {
            Builders<Post>.Filter.Text(query),
            Builders<Post>.Filter.Eq(p => p.IsDeleted, false)
        };

        if (excludedHandles.Count > 0)
        {
            var excludedHandleValues = excludedHandles.Select(h => h.Value).ToList();
            var excludedAuthorIds = await context.Users
                .Find(Builders<User>.Filter.In("handle", excludedHandleValues))
                .Project(u => u.Id)
                .ToListAsync(ct);

            if (excludedAuthorIds.Count > 0)
                filters.Add(Builders<Post>.Filter.Nin(p => p.AuthorId, excludedAuthorIds));
        }

        var filter = Builders<Post>.Filter.And(filters);

        var results = await context.Posts
            .Find(filter)
            .Sort(Builders<Post>.Sort.MetaTextScore("textScore").Descending(p => p.PostedAt))
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(ct);

        return results;
    }
}
