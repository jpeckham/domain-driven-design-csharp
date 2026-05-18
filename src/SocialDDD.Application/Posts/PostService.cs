using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts;

public sealed class PostService(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher,
    IPendingMediaStore pendingMediaStore)
{
    public async Task<PostDto> CreateAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var authorId = UserId.From(request.AuthorId);

        if (!await userRepository.ExistsByIdAsync(authorId, ct))
            throw new DomainException("Author not found.");

        var media = LoadAndValidateMedia(request.MediaAssetIds);
        var post = Post.Create(authorId, new PostContent(request.Content), media);

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
        int skip, int limit, Guid? requesterId = null, bool rootOnly = false, CancellationToken ct = default)
    {
        var posts = await postRepository.GetFeedAsync(skip, limit, rootOnly, ct);
        var (handle, userId) = await ResolveRequesterAsync(requesterId, ct);
        return await ToDtosAsync(posts, handle, userId, ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorAsync(
        Guid userId, Guid? requesterId = null, CancellationToken ct = default)
    {
        var posts = await postRepository.GetByAuthorAsync(UserId.From(userId), ct);
        var (handle, requesterUserId) = await ResolveRequesterAsync(requesterId, ct);
        return await ToDtosAsync(posts, handle, requesterUserId, ct);
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
            mediaDtos);
    }

    private async Task<IReadOnlyList<PostDto>> ToDtosAsync(
        IReadOnlyList<Post> posts, Handle? requesterHandle, UserId? requesterUserId, CancellationToken ct)
    {
        var dtos = new List<PostDto>(posts.Count);
        foreach (var post in posts)
            dtos.Add(await ToDtoAsync(post, requesterHandle, requesterUserId, ct));
        return dtos;
    }
}
