using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts.Commands;

public sealed record CreateRepostCommand(Guid OriginalPostId, Guid RequesterId, string? Commentary);

public sealed class CreateRepostCommandHandler(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<PostDto> HandleAsync(CreateRepostCommand command, CancellationToken ct = default)
    {
        var originalPostId = PostId.From(command.OriginalPostId);
        var requesterId = UserId.From(command.RequesterId);

        var originalPost = await postRepository.GetByIdAsync(originalPostId, ct)
            ?? throw new DomainException($"Post {command.OriginalPostId} not found.");

        var reposter = await userRepository.GetByIdAsync(requesterId, ct)
            ?? throw new DomainException($"User {command.RequesterId} not found.");

        var originalAuthor = await userRepository.GetByIdAsync(originalPost.AuthorId, ct)
            ?? throw new DomainException("Original post author not found.");

        var existing = await postRepository.FindRepostAsync(originalPostId, requesterId, ct);
        if (existing is not null)
            throw new DuplicateRepostException("You have already reposted this post.");

        var repost = Post.CreateRepost(
            originalPost,
            originalAuthor.Handle,
            requesterId,
            reposter.Handle,
            command.Commentary);

        await postRepository.AddAsync(repost, ct);
        await eventDispatcher.DispatchAsync(repost.PopDomainEvents(), ct);

        int origRepostCount = await postRepository.GetRepostCountAsync(originalPost.Id, ct);
        bool origLikedByMe = await postRepository.IsLikedByAsync(originalPost.Id, reposter.Handle, ct);
        var originalPostDto = new PostDto(
            originalPost.Id.Value,
            originalPost.AuthorId.Value,
            originalPost.Content?.Value,
            originalPost.PostedAt,
            originalPost.LikeCount,
            origLikedByMe,
            originalPost.ParentPostId?.Value,
            0,
            originalPost.Mentions.Select(h => h.Value).ToList(),
            originalPost.Hashtags.ToList(),
            originalPost.OriginalPostId?.Value,
            origRepostCount,
            false,
            null,
            ToMediaDtos(originalPost));

        return new PostDto(
            repost.Id.Value,
            repost.AuthorId.Value,
            repost.Content?.Value,
            repost.PostedAt,
            repost.LikeCount,
            false,
            repost.ParentPostId?.Value,
            0,
            repost.Mentions.Select(h => h.Value).ToList(),
            repost.Hashtags.ToList(),
            repost.OriginalPostId?.Value,
            0,
            false,
            originalPostDto);
    }

    private static List<PostMediaDto>? ToMediaDtos(Post post) =>
        post.Media.Count == 0
            ? null
            : post.Media
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
                .ToList();
}
