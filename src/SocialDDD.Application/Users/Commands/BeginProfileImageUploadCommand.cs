using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed record BeginProfileImageUploadCommand(Guid UserId, string ContentType);

public sealed class BeginProfileImageUploadCommandHandler(
    IUserRepository userRepository,
    IProfileImageStorageService storageService)
{
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<(Guid AssetId, string UploadUrl)> HandleAsync(
        BeginProfileImageUploadCommand command, CancellationToken ct = default)
    {
        if (!AllowedTypes.Contains(command.ContentType))
            throw new DomainValidationException($"Content type '{command.ContentType}' is not allowed. Use image/jpeg, image/png, or image/webp.");

        _ = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct)
            ?? throw new DomainException($"User {command.UserId} not found.");

        var assetId = Guid.NewGuid();
        var (uploadUrl, _) = await storageService.ReserveUploadAsync(assetId, command.ContentType, ct);
        return (assetId, uploadUrl);
    }
}
