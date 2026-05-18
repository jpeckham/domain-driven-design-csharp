namespace SocialDDD.Application.Posts.DTOs;

public sealed record PostDto(
    Guid PostId,
    Guid AuthorId,
    string? Content,
    DateTime PostedAt,
    int LikeCount,
    bool LikedByMe,
    Guid? ParentPostId = null,
    int ReplyCount = 0,
    IReadOnlyList<string>? Mentions = null,
    IReadOnlyList<string>? Hashtags = null,
    Guid? OriginalPostId = null,
    int RepostCount = 0,
    bool IsRepostedByMe = false,
    PostDto? OriginalPost = null);
