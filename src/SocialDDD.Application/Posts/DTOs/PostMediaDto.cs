namespace SocialDDD.Application.Posts.DTOs;

public sealed record PostMediaDto(
    Guid AssetId,
    string Kind,
    string? AltText,
    int? Width,
    int? Height,
    long? DurationMs,
    string MediaUrl,
    int SortOrder);
