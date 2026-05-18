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

        return await ToDtoAsync(post, null, ct);
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
        int skip, int limit, string? requesterHandle = null, bool rootOnly = false, CancellationToken ct = default)
    {
        var posts = await postRepository.GetFeedAsync(skip, limit, rootOnly, ct);
        return await ToDtosAsync(posts, requesterHandle, ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorAsync(
        Guid userId, string? requesterHandle = null, CancellationToken ct = default)
    {
        var posts = await postRepository.GetByAuthorAsync(UserId.From(userId), ct);
        return await ToDtosAsync(posts, requesterHandle, ct);
    }

    public async Task<string?> GetHandleByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(userId), ct);
        return user?.Handle.Value;
    }

    private async Task<PostDto> ToDtoAsync(Post post, Handle? requesterHandle, CancellationToken ct)
    {
        bool likedByMe = requesterHandle is not null
            && await postRepository.IsLikedByAsync(post.Id, requesterHandle, ct);
        int replyCount = await postRepository.CountRepliesAsync(post.Id, ct);
        return new PostDto(
            post.Id.Value,
            post.AuthorId.Value,
            post.Content.Value,
            post.PostedAt,
            post.LikeCount,
            likedByMe,
            post.ParentPostId?.Value,
            replyCount,
            post.Mentions.Select(h => h.Value).ToList(),
            post.Hashtags.ToList());
    }

    private async Task<IReadOnlyList<PostDto>> ToDtosAsync(
        IReadOnlyList<Post> posts, string? requesterHandle, CancellationToken ct)
    {
        Handle? handle = requesterHandle is not null ? new Handle(requesterHandle) : null;

        var dtos = new List<PostDto>(posts.Count);
        foreach (var post in posts)
        {
            dtos.Add(await ToDtoAsync(post, handle, ct));
        }
        return dtos;
    }
}
