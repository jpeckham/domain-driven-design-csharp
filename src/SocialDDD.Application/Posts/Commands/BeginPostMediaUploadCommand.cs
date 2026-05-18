using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Application.Posts.Commands;

public sealed record BeginPostMediaUploadCommand(Guid UserId, string ContentType);

public sealed class BeginPostMediaUploadCommandHandler(
    IPostMediaStorageService storageService,
    IPendingMediaStore pendingMediaStore)
{
    private static readonly HashSet<string> AllowedTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4"
    ];

    public async Task<(Guid AssetId, string UploadUrl)> HandleAsync(
        BeginPostMediaUploadCommand command, CancellationToken ct = default)
    {
        if (!AllowedTypes.Contains(command.ContentType))
            throw new DomainValidationException(
                $"Content type '{command.ContentType}' is not allowed. " +
                "Use image/jpeg, image/png, image/webp, image/gif, or video/mp4.");

        var assetId = Guid.NewGuid();
        var (uploadUrl, _) = await storageService.ReserveUploadAsync(assetId, command.ContentType, ct);
        pendingMediaStore.Reserve(assetId);
        return (assetId, uploadUrl);
    }
}
