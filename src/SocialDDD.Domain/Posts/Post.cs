using System.Text.RegularExpressions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts.Events;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts;

public sealed partial class Post : AggregateRoot<PostId>
{
    [GeneratedRegex(@"@([a-zA-Z0-9_]{1,30})")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"#([a-zA-Z0-9_]+)")]
    private static partial Regex HashtagPattern();

    public UserId AuthorId { get; private set; } = null!;
    public PostContent? Content { get; private set; }
    public DateTime PostedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public PostId? ParentPostId { get; private set; }
    public PostId? OriginalPostId { get; private set; }

    public HashSet<Handle> LikedBy { get; private set; } = new();
    public HashSet<Handle> Mentions { get; private set; } = new();
    public HashSet<string> Hashtags { get; private set; } = new();

    public int LikeCount => LikedBy.Count;
    public List<PostMedia> Media { get; private set; } = [];

    private Post() { }

    public static Post Create(
        UserId authorId, PostContent content, IReadOnlyList<PostMedia>? media = null)
    {
        var post = new Post
        {
            Id = PostId.New(),
            AuthorId = authorId,
            Content = content,
            PostedAt = DateTime.UtcNow,
            IsDeleted = false,
            ParentPostId = null
        };
        post.ExtractMentionsAndHashtags(authorId: null);
        if (media is { Count: > 0 })
            post.AttachMedia(media);
        post.RaiseDomainEvent(new PostCreated(post.Id, authorId));
        return post;
    }

    public static Post CreateReply(
        PostId parentPostId, UserId authorId, Handle authorHandle, PostContent content,
        IReadOnlyList<PostMedia>? media = null)
    {
        var post = new Post
        {
            Id = PostId.New(),
            AuthorId = authorId,
            Content = content,
            PostedAt = DateTime.UtcNow,
            IsDeleted = false,
            ParentPostId = parentPostId
        };
        post.ExtractMentionsAndHashtags(authorId: authorHandle);
        if (media is { Count: > 0 })
            post.AttachMedia(media);
        post.RaiseDomainEvent(new PostCreated(post.Id, authorId));
        return post;
    }

    public static Post CreateRepost(
        Post originalPost,
        Handle originalAuthorHandle,
        UserId reposterUserId,
        Handle reposterHandle,
        string? commentary)
    {
        if (originalPost.IsDeleted)
            throw new DomainException("Cannot repost a deleted post.");
        if (originalPost.OriginalPostId is not null)
            throw new DomainException("Cannot repost a repost.");
        if (reposterHandle == originalAuthorHandle)
            throw new DomainException("Cannot repost your own post.");
        if (commentary is not null && commentary.Length > 280)
            throw new DomainValidationException("Repost commentary must be 280 characters or fewer.");

        PostContent? content = commentary is not null ? new PostContent(commentary) : null;

        var post = new Post
        {
            Id = PostId.New(),
            AuthorId = reposterUserId,
            Content = content,
            PostedAt = DateTime.UtcNow,
            IsDeleted = false,
            OriginalPostId = originalPost.Id
        };

        post.RaiseDomainEvent(new PostReposted(originalPost.Id, reposterHandle));
        return post;
    }

    private void ExtractMentionsAndHashtags(Handle? authorId)
    {
        Mentions = new HashSet<Handle>(
            MentionPattern()
                .Matches(Content!.Value)
                .Select(m => m.Groups[1].Value.ToLowerInvariant())
                .Where(v => authorId is null || v != authorId.Value)
                .Select(v => new Handle(v)));

        Hashtags = new HashSet<string>(
            HashtagPattern()
                .Matches(Content!.Value)
                .Select(m => m.Groups[1].Value.ToLowerInvariant()));
    }

    public void Delete()
    {
        if (IsDeleted)
            throw new DomainException("Post is already deleted.");

        IsDeleted = true;
        RaiseDomainEvent(new PostDeleted(Id, AuthorId));
    }

    public void Like(Handle byHandle)
    {
        if (IsDeleted)
            throw new DomainValidationException("Cannot like a deleted post.");

        if (!LikedBy.Add(byHandle))
            throw new AlreadyLikedException($"Post is already liked by {byHandle.Display}.");

        RaiseDomainEvent(new PostLiked(Id, byHandle));
    }

    public void Unlike(Handle byHandle)
    {
        if (!LikedBy.Remove(byHandle))
            throw new NotLikedException($"Post is not liked by {byHandle.Display}.");

        RaiseDomainEvent(new PostUnliked(Id, byHandle));
    }

    public void AttachMedia(IReadOnlyList<PostMedia> media)
    {
        if (media.Count > 4)
            throw new DomainException("A post may contain at most 4 media items.");
        if (media.Count > 1 && media.Any(m => m.Kind != media[0].Kind))
            throw new DomainException("All media items must be the same kind.");
        if (media[0].Kind == MediaKind.Video && media.Count > 1)
            throw new DomainException("A post may contain at most 1 video.");

        Media = media.Select((m, i) => m with { SortOrder = i }).ToList();
    }
}
