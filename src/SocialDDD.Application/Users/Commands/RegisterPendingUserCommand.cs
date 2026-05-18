using System.Security.Cryptography;
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

        var existingUser = await userRepository.GetByEmailAsync(email, ct);
        if (existingUser is not null)
        {
            if (existingUser.Status != UserStatus.Pending)
                throw new DomainException("Email is already registered.");

            await SendVerificationCodeAsync(existingUser, email, ct);
            return;
        }

        var username = new Username(request.Username);
        var handle = new Handle(request.Handle);
        var displayName = new DisplayName(request.DisplayName);

        if (await userRepository.ExistsByUsernameAsync(username, ct))
            throw new DomainException("Username is already taken.");

        if (await userRepository.HandleExistsAsync(handle, ct))
            throw new DomainException("Handle is already taken.");

        var hash = passwordHasher.Hash(request.Password);
        var user = User.Register(username, email, new PasswordHash(hash), handle, displayName);

        await userRepository.AddAsync(user, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        await SendVerificationCodeAsync(user, email, ct);
    }

    private async Task SendVerificationCodeAsync(User user, Email email, CancellationToken ct)
    {
        var code = new VerificationCode(
            GenerateCode(),
            DateTimeOffset.UtcNow.AddMinutes(15));

        await codeRepository.SaveAsync(user.Id, code, ct);
        await emailService.SendVerificationEmailAsync(email.Value, code.Code, ct);
    }

    private static string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
