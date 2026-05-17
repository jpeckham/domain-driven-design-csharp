namespace SocialDDD.Application.Posts.DTOs;

public sealed record PostDto(Guid PostId, Guid AuthorId, string Content, DateTime PostedAt);
