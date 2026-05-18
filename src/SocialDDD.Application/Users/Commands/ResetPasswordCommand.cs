using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class ResetPasswordCommand(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IPasswordHasher passwordHasher,
    IDomainEventDispatcher eventDispatcher)
{
    private const int MinPasswordLength = 8;

    public async Task ExecuteAsync(string token, string newPassword, CancellationToken ct = default)
    {
        var record = await tokenRepository.FindByTokenAsync(token, ct);
        if (record is null)
            throw new DomainValidationException("Invalid or expired password reset token.");

        var (userId, tokenRecord) = record.Value;

        if (tokenRecord.IsExpired(DateTimeOffset.UtcNow))
            throw new DomainValidationException("Password reset token has expired.");

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength)
            throw new DomainValidationException($"Password must be at least {MinPasswordLength} characters.");

        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new DomainValidationException("Invalid or expired password reset token.");

        var newHash = passwordHasher.Hash(newPassword);
        user.ResetPassword(new PasswordHash(newHash));

        await userRepository.UpdateAsync(user, ct);
        await tokenRepository.DeleteByUserIdAsync(userId, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);
    }
}
