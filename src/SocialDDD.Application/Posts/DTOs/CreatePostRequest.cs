namespace SocialDDD.Application.Posts.DTOs;

public sealed record CreatePostRequest(Guid AuthorId, string Content);
