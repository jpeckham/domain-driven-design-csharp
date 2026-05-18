using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts.Commands;

public sealed record CreateReplyCommand(Guid ParentPostId, Guid AuthorUserId, string Content);

public sealed class CreateReplyCommandHandler(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<PostDto> HandleAsync(CreateReplyCommand command, CancellationToken ct = default)
    {
        var parentPostId = PostId.From(command.ParentPostId);

        var parentPost = await postRepository.GetByIdAsync(parentPostId, ct)
            ?? throw new DomainException($"Post {command.ParentPostId} not found.");

        if (parentPost.IsDeleted)
            throw new DomainException("Cannot reply to a deleted post.");

        var author = await userRepository.GetByIdAsync(UserId.From(command.AuthorUserId), ct)
            ?? throw new DomainException($"User {command.AuthorUserId} not found.");

        var authorHandle = author.Handle;

        // Auto-prefix with @parentAuthorHandle if not already starting with it
        var parentAuthor = await userRepository.GetByIdAsync(parentPost.AuthorId, ct);
        string content = command.Content;
        if (parentAuthor is not null)
        {
            var prefix = $"@{parentAuthor.Handle.Value} ";
            if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                content = prefix + content;
        }

        var postContent = new PostContent(content);
        var reply = Post.CreateReply(parentPostId, UserId.From(command.AuthorUserId), authorHandle, postContent);

        await postRepository.AddAsync(reply, ct);
        await eventDispatcher.DispatchAsync(reply.PopDomainEvents(), ct);

        return new PostDto(
            reply.Id.Value,
            reply.AuthorId.Value,
            reply.Content.Value,
            reply.PostedAt,
            reply.LikeCount,
            false,
            reply.ParentPostId?.Value,
            0,
            reply.Mentions.Select(h => h.Value).ToList(),
            reply.Hashtags.ToList());
    }
}
