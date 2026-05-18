namespace SocialDDD.Domain.Users;

public sealed record ProfileImage(
    Guid AssetId,
    string StorageKey,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    DateTimeOffset UploadedAt);
