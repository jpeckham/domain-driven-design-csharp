using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Application.Social.Posts.Commands;

public sealed record LikePostCommand(Guid PostId, Guid RequesterId);

public sealed class LikePostCommandHandler(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<int> HandleAsync(LikePostCommand command, CancellationToken ct = default)
    {
        var postId = PostId.From(command.PostId);

        var user = await userRepository.GetByIdAsync(UserId.From(command.RequesterId), ct)
            ?? throw new DomainException($"User {command.RequesterId} not found.");

        var handle = user.Handle;

        var post = await postRepository.GetByIdAsync(postId, ct)
            ?? throw new DomainException($"Post {command.PostId} not found.");

        post.Like(handle);

        await postRepository.AddLikeAsync(postId, handle, ct);
        await eventDispatcher.DispatchAsync(post.PopDomainEvents(), ct);

        return post.LikeCount;
    }
}
