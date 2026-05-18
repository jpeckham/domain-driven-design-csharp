namespace SocialDDD.Application.Posts.DTOs;

public sealed record PostConversationDto(PostDto Post, List<PostConversationDto> Replies);
