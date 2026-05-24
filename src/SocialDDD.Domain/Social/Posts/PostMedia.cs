using SocialDDD.Domain.Social.Profiles;
namespace SocialDDD.Domain.Social.Posts;

public sealed record PostMedia(
    Guid AssetId,
    MediaKind Kind,
    string StorageKey,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    long? DurationMs,
    string? ThumbnailKey,
    string? AltText,
    int SortOrder);
