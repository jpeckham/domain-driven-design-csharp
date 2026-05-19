using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Application.Posts.Queries;
using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Follows;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts;

public sealed class PostService(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IBlockRepository blockRepository,
    IFollowRepository followRepository,
    IDomainEventDispatcher eventDispatcher,
    IPendingMediaStore pendingMediaStore)
{
    private const int DefaultSearchLimit = 20;
    private const int MaxSearchLimit = 100;

    public async Task<PostDto> CreateAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var authorId = UserId.From(request.AuthorId);

        if (!await userRepository.ExistsByIdAsync(authorId, ct))
            throw new DomainException("Author not found.");

        var media = LoadAndValidateMedia(request.MediaAssetIds);
        var content = string.IsNullOrWhiteSpace(request.Content)
            ? null
            : new PostContent(request.Content);
        var post = Post.Create(authorId, content, media);

        await postRepository.AddAsync(post, ct);
        await eventDispatcher.DispatchAsync(post.PopDomainEvents(), ct);

        return await ToDtoAsync(post, null, null, ct);
    }

    public async Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken ct = default)
    {
        var post = await postRepository.GetByIdAsync(PostId.From(postId), ct)
            ?? throw new DomainException($"Post {postId} not found.");

        if (post.AuthorId != UserId.From(requesterId))
            throw new DomainException("Only the author can delete their post.");

        post.Delete();

        await postRepository.UpdateAsync(post, ct);
        await eventDispatcher.DispatchAsync(post.PopDomainEvents(), ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(
        int skip, int limit, Guid? requesterId = null, bool rootOnly = false, bool followingOnly = false, CancellationToken ct = default)
    {
        var (handle, userId) = await ResolveRequesterAsync(requesterId, ct);
        var excludedHandles = await GetExcludedHandlesAsync(handle, ct);
        IReadOnlySet<Handle>? includedHandles = null;
        if (followingOnly)
        {
            if (handle is null) return [];
            includedHandles = (await followRepository.GetFollowedHandlesAsync(handle, ct)).ToHashSet();
            if (includedHandles.Count == 0) return [];
        }
        var safeLimit = limit <= 0 ? 20 : Math.Min(limit, 100);
        var safeSkip = Math.Max(skip, 0);
        var posts = await postRepository.GetFeedAsync(safeSkip, safeLimit, rootOnly, excludedHandles, includedHandles, ct);
        return await ToDtosAsync(posts, handle, userId, ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorAsync(
        Guid userId, Guid? requesterId = null, CancellationToken ct = default)
    {
        return await GetByAuthorAsync(userId, limit: 20, offset: 0, requesterId, ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorAsync(
        Guid userId, int limit, int offset, Guid? requesterId = null, CancellationToken ct = default)
    {
        var (handle, requesterUserId) = await ResolveRequesterAsync(requesterId, ct);
        var author = await userRepository.GetByIdAsync(UserId.From(userId), ct);
        if (author is not null && await IsExcludedAsync(handle, author.Handle, ct))
            return [];
        var safeLimit = limit <= 0 ? 20 : Math.Min(limit, 100);
        var safeOffset = Math.Max(offset, 0);
        var posts = await postRepository.GetByAuthorAsync(UserId.From(userId), safeLimit, safeOffset, ct);
        return await ToDtosAsync(posts, handle, requesterUserId, ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorHandleAsync(
        string rawHandle,
        int limit,
        int offset,
        Guid? requesterId = null,
        CancellationToken ct = default)
    {
        var author = await userRepository.FindByHandleAsync(new Handle(rawHandle), ct)
            ?? throw new DomainException($"User with handle @{new Handle(rawHandle).Value} not found.");

        var safeLimit = limit <= 0 ? 20 : Math.Min(limit, 100);
        var safeOffset = Math.Max(offset, 0);
        var (handle, requesterUserId) = await ResolveRequesterAsync(requesterId, ct);
        if (await IsExcludedAsync(handle, author.Handle, ct))
            return [];
        var posts = await postRepository.GetByAuthorAsync(author.Id, safeLimit, safeOffset, ct);
        return await ToDtosAsync(posts, handle, requesterUserId, ct);
    }

    public async Task<SearchResultsDto> SearchAsync(SearchPostsQuery query, CancellationToken ct = default)
    {
        var trimmedQuery = query.Query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
            throw new DomainValidationException("Search query is required.");
        if (trimmedQuery.Length > 200)
            throw new DomainValidationException("Search query must be 200 characters or fewer.");

        var limit = query.Limit <= 0 ? DefaultSearchLimit : Math.Min(query.Limit, MaxSearchLimit);
        var offset = query.Offset < 0 ? 0 : query.Offset;

        var requesterUser = query.RequesterHandle is not null
            ? await userRepository.FindByHandleAsync(query.RequesterHandle, ct)
            : null;

        var excludedHandles = await GetExcludedHandlesAsync(query.RequesterHandle, ct);
        var posts = await postRepository.SearchAsync(trimmedQuery, query.RequesterHandle, excludedHandles, limit, offset, ct);
        var dtos = await ToDtosAsync(posts, query.RequesterHandle, requesterUser?.Id, ct);

        return new SearchResultsDto(dtos.ToList(), trimmedQuery, limit, offset);
    }

    public async Task<string?> GetHandleByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(userId), ct);
        return user?.Handle.Value;
    }

    private IReadOnlyList<PostMedia>? LoadAndValidateMedia(IReadOnlyList<Guid>? assetIds)
    {
        if (assetIds is null or { Count: 0 }) return null;

        if (assetIds.Count > 4)
            throw new DomainValidationException("A post may contain at most 4 media items.");

        var result = new List<PostMedia>(assetIds.Count);
        foreach (var id in assetIds)
        {
            if (!pendingMediaStore.TryGetCompleted(id, out var media))
                throw new DomainException($"Media asset {id} not found or not yet uploaded.");
            result.Add(media!);
        }

        var kinds = result.Select(m => m.Kind).Distinct().ToList();
        if (kinds.Count > 1)
            throw new DomainValidationException("All media items must be the same kind.");
        if (kinds[0] == MediaKind.Video && result.Count > 1)
            throw new DomainValidationException("A post may contain at most 1 video.");

        return result;
    }

    private async Task<(Handle? handle, UserId? userId)> ResolveRequesterAsync(Guid? requesterId, CancellationToken ct)
    {
        if (requesterId is null) return (null, null);
        var userId = UserId.From(requesterId.Value);
        var user = await userRepository.GetByIdAsync(userId, ct);
        return (user?.Handle, userId);
    }

    private async Task<PostDto> ToDtoAsync(Post post, Handle? requesterHandle, UserId? requesterUserId, CancellationToken ct)
    {
        var author = await userRepository.GetByIdAsync(post.AuthorId, ct);
        bool likedByMe = requesterHandle is not null
            && await postRepository.IsLikedByAsync(post.Id, requesterHandle, ct);
        int replyCount = await postRepository.CountRepliesAsync(post.Id, ct);
        int repostCount = await postRepository.GetRepostCountAsync(post.Id, ct);
        bool isRepostedByMe = requesterUserId is not null
            && await postRepository.FindRepostAsync(post.Id, requesterUserId, ct) is not null;

        PostDto? originalPost = null;
        if (post.OriginalPostId is not null)
        {
            var orig = await postRepository.GetByIdAsync(post.OriginalPostId, ct);
            if (orig is not null)
                originalPost = await ToDtoAsync(orig, requesterHandle, requesterUserId, ct);
        }

        var mediaDtos = post.Media.Count > 0
            ? post.Media
                .OrderBy(m => m.SortOrder)
                .Select(m => new PostMediaDto(
                    m.AssetId,
                    m.Kind.ToString(),
                    m.AltText,
                    m.Width,
                    m.Height,
                    m.DurationMs,
                    $"/api/post-media/{m.AssetId}",
                    m.SortOrder))
                .ToList()
            : null;

        return new PostDto(
            post.Id.Value,
            post.AuthorId.Value,
            post.Content?.Value,
            post.PostedAt,
            post.LikeCount,
            likedByMe,
            post.ParentPostId?.Value,
            replyCount,
            post.Mentions.Select(h => h.Value).ToList(),
            post.Hashtags.ToList(),
            post.OriginalPostId?.Value,
            repostCount,
            isRepostedByMe,
            originalPost,
            mediaDtos,
            author?.DisplayName.Value ?? "Unknown",
            author?.Handle.Display ?? "@unknown",
            author?.ProfileImage is null ? null : $"/api/profile-images/{author.ProfileImage.AssetId}");
    }

    private async Task<IReadOnlyList<PostDto>> ToDtosAsync(
        IReadOnlyList<Post> posts, Handle? requesterHandle, UserId? requesterUserId, CancellationToken ct)
    {
        var dtos = new List<PostDto>(posts.Count);
        foreach (var post in posts)
            dtos.Add(await ToDtoAsync(post, requesterHandle, requesterUserId, ct));
        return dtos;
    }

    private async Task<bool> IsExcludedAsync(Handle? requesterHandle, Handle authorHandle, CancellationToken ct)
    {
        if (requesterHandle is null)
            return false;

        return await blockRepository.IsBlockedAsync(requesterHandle, authorHandle, ct)
            || await blockRepository.IsBlockedAsync(authorHandle, requesterHandle, ct);
    }

    private async Task<IReadOnlySet<Handle>> GetExcludedHandlesAsync(Handle? requesterHandle, CancellationToken ct)
    {
        var excludedHandles = new HashSet<Handle>();
        if (requesterHandle is null)
            return excludedHandles;

        foreach (var handle in await blockRepository.GetBlockedHandlesAsync(requesterHandle, ct))
            excludedHandles.Add(handle);

        foreach (var handle in await blockRepository.GetBlockerHandlesAsync(requesterHandle, ct))
            excludedHandles.Add(handle);

        return excludedHandles;
    }
}
