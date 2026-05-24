namespace SocialDDD.Domain.Social.Profiles;

public sealed record ProfileImage(
    Guid AssetId,
    string StorageKey,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    DateTimeOffset UploadedAt);
