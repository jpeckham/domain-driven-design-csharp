namespace SocialDDD.Application.Posts.DTOs;

public sealed record SearchResultsDto(
    List<PostDto> Posts,
    string Query,
    int Limit,
    int Offset);
