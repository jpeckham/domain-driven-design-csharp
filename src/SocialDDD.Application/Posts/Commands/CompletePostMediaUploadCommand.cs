using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Application.Posts.Commands;

public sealed record CompletePostMediaUploadCommand(
    Guid AssetId,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    long? DurationMs,
    string? AltText);

public sealed class CompletePostMediaUploadCommandHandler(IPendingMediaStore pendingMediaStore)
{
    public Task<PendingMediaDto> HandleAsync(
        CompletePostMediaUploadCommand command, CancellationToken ct = default)
    {
        if (!pendingMediaStore.IsReserved(command.AssetId))
            throw new DomainValidationException(
                $"Media asset {command.AssetId} was not reserved. Call the upload-sessions endpoint first.");

        var kind = command.ContentType.StartsWith("video/")
            ? MediaKind.Video
            : MediaKind.Image;

        var media = new PostMedia(
            command.AssetId,
            kind,
            command.AssetId.ToString(),
            command.ContentType,
            command.ByteLength,
            command.Width,
            command.Height,
            command.DurationMs,
            null,
            command.AltText,
            0);

        pendingMediaStore.Complete(command.AssetId, media);

        return Task.FromResult(new PendingMediaDto(
            command.AssetId,
            kind.ToString(),
            command.Width,
            command.Height,
            command.DurationMs,
            command.AltText));
    }
}
