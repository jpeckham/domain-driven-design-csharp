namespace SocialDDD.Application.Social.Posts.DTOs;

public sealed record PostConversationDto(PostDto Post, List<PostConversationDto> Replies, PostDto? ParentPost = null);
