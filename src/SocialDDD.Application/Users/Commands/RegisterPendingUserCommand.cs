using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class RegisterPendingUserCommand(
    IUserRepository userRepository,
    IVerificationCodeRepository codeRepository,
    IPasswordHasher passwordHasher,
    IEmailService emailService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task ExecuteAsync(RegisterPendingRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var username = new Username(request.Username);
        var handle = new Handle(request.Handle);
        var displayName = new DisplayName(request.DisplayName);

        if (await userRepository.ExistsByEmailAsync(email, ct))
            throw new DomainException("Email is already registered.");

        if (await userRepository.ExistsByUsernameAsync(username, ct))
            throw new DomainException("Username is already taken.");

        if (await userRepository.HandleExistsAsync(handle, ct))
            throw new DomainException("Handle is already taken.");

        var hash = passwordHasher.Hash(request.Password);
        var user = User.Register(username, email, new PasswordHash(hash), handle, displayName);

        await userRepository.AddAsync(user, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        var code = new VerificationCode(
            GenerateCode(),
            DateTimeOffset.UtcNow.AddMinutes(15));

        await codeRepository.SaveAsync(user.Id, code, ct);
        await emailService.SendVerificationEmailAsync(email.Value, code.Code, ct);
    }

    private static string GenerateCode()
    {
        var value = Random.Shared.Next(0, 1_000_000);
        return value.ToString("D6");
    }
}
