using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed record CompleteProfileImageUploadCommand(
    Guid UserId,
    Guid AssetId,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height);

public sealed class CompleteProfileImageUploadCommandHandler(
    IUserRepository userRepository,
    IProfileImageStorageService storageService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task HandleAsync(CompleteProfileImageUploadCommand command, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct)
            ?? throw new DomainException($"User {command.UserId} not found.");

        var storageKey = command.AssetId.ToString();
        if (!await storageService.ExistsAsync(storageKey, ct))
            throw new DomainException("Uploaded profile image was not found.");

        if (user.ProfileImage is { } oldImage)
            await storageService.DeleteAsync(oldImage.StorageKey, ct);

        var profileImage = new ProfileImage(
            command.AssetId,
            storageKey,
            command.ContentType,
            command.ByteLength,
            command.Width,
            command.Height,
            DateTimeOffset.UtcNow);

        user.SetProfileImage(profileImage);
        await userRepository.UpdateAsync(user, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);
    }
}
