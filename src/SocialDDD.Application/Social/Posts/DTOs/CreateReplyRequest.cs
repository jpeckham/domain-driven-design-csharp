namespace SocialDDD.Application.Social.Posts.DTOs;

public sealed record CreateReplyRequest(string Content, IReadOnlyList<Guid>? MediaAssetIds = null);
