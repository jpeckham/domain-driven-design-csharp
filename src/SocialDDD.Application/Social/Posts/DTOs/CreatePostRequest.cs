namespace SocialDDD.Application.Social.Posts.DTOs;

public sealed record CreatePostRequest(
    Guid AuthorId,
    string Content,
    IReadOnlyList<Guid>? MediaAssetIds = null);
