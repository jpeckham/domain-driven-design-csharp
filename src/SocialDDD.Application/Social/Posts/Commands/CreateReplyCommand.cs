using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Social.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;
using System.Text.RegularExpressions;

namespace SocialDDD.Application.Social.Posts.Commands;

public sealed record CreateReplyCommand(
    Guid ParentPostId,
    Guid AuthorUserId,
    string Content,
    IReadOnlyList<Guid>? MediaAssetIds = null);

public sealed class CreateReplyCommandHandler(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher,
    IPendingMediaStore pendingMediaStore)
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

        var parentAuthor = await userRepository.GetByIdAsync(parentPost.AuthorId, ct);
        string content = command.Content;
        if (parentAuthor is not null)
        {
            var prefix = $"@{parentAuthor.Handle.Value} ";
            content = prefix + StripLeadingMention(content);
        }

        var media = LoadAndValidateMedia(command.MediaAssetIds);
        var postContent = new PostContent(content);
        var ancestorPostIds = parentPost.AncestorPostIds.Concat([parentPost.Id]).ToList();
        var reply = Post.CreateReply(
            parentPostId,
            UserId.From(command.AuthorUserId),
            authorHandle,
            postContent,
            media,
            ancestorPostIds);

        await postRepository.AddAsync(reply, ct);
        await postRepository.IncrementReplyCountsAsync(ancestorPostIds, ct);
        await eventDispatcher.DispatchAsync(reply.PopDomainEvents(), ct);

        var mediaDtos = reply.Media.Count > 0
            ? reply.Media
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
            reply.Id.Value,
            reply.AuthorId.Value,
            reply.Content?.Value,
            reply.PostedAt,
            reply.LikeCount,
            false,
            reply.ParentPostId?.Value,
            0,
            reply.Mentions.Select(h => h.Value).ToList(),
            reply.Hashtags.ToList(),
            null,
            0,
            false,
            null,
            mediaDtos,
            author.DisplayName.Value,
            author.Handle.Display,
            author.ProfileImage is null ? null : $"/api/profile-images/{author.ProfileImage.AssetId}");
    }

    private static string StripLeadingMention(string content) =>
        Regex.Replace(content.TrimStart(), @"^@[a-zA-Z0-9_]{1,30}\s*", "");

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
}
