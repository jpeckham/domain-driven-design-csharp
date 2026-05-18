namespace SocialDDD.Application.Posts.DTOs;

public sealed record PendingMediaDto(
    Guid AssetId,
    string Kind,
    int? Width,
    int? Height,
    long? DurationMs,
    string? AltText);
