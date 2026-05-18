using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts;

public sealed class PostService(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<PostDto> CreateAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var authorId = UserId.From(request.AuthorId);

        if (!await userRepository.ExistsByIdAsync(authorId, ct))
            throw new DomainException("Author not found.");

        var post = Post.Create(authorId, new PostContent(request.Content));

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
            originalPost);
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
