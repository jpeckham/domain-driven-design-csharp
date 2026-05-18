using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed record RemoveProfileImageCommand(Guid UserId);

public sealed class RemoveProfileImageCommandHandler(
    IUserRepository userRepository,
    IProfileImageStorageService storageService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task HandleAsync(RemoveProfileImageCommand command, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(command.UserId), ct)
            ?? throw new DomainException($"User {command.UserId} not found.");

        var storageKey = user.ProfileImage?.StorageKey
            ?? throw new DomainValidationException("User does not have a profile image.");

        user.RemoveProfileImage();
        await storageService.DeleteAsync(storageKey, ct);
        await userRepository.UpdateAsync(user, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);
    }
}
