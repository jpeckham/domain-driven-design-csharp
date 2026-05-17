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

        return ToDto(post);
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

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(int skip, int limit, CancellationToken ct = default)
    {
        var posts = await postRepository.GetFeedAsync(skip, limit, ct);
        return posts.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorAsync(Guid userId, CancellationToken ct = default)
    {
        var posts = await postRepository.GetByAuthorAsync(UserId.From(userId), ct);
        return posts.Select(ToDto).ToList();
    }

    private static PostDto ToDto(Post post) =>
        new(post.Id.Value, post.AuthorId.Value, post.Content.Value, post.PostedAt);
}
