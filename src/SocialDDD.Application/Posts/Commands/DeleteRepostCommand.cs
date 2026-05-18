using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts.Commands;

public sealed record DeleteRepostCommand(Guid OriginalPostId, Guid RequesterId);

public sealed class DeleteRepostCommandHandler(
    IPostRepository postRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task HandleAsync(DeleteRepostCommand command, CancellationToken ct = default)
    {
        var originalPostId = PostId.From(command.OriginalPostId);
        var requesterId = UserId.From(command.RequesterId);

        var repost = await postRepository.FindRepostAsync(originalPostId, requesterId, ct)
            ?? throw new DomainException("Repost not found.");

        repost.Delete();

        await postRepository.UpdateAsync(repost, ct);
        await eventDispatcher.DispatchAsync(repost.PopDomainEvents(), ct);
    }
}
