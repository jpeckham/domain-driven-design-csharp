namespace SocialDDD.Application.Posts.DTOs;

public sealed record CreateReplyRequest(string Content, IReadOnlyList<Guid>? MediaAssetIds = null);
