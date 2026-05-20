using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Users;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Application.Users.Commands;

public sealed class RequestPasswordResetCommand(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task ExecuteAsync(string email, CancellationToken ct = default)
    {
        Email emailVO;
        try
        {
            emailVO = new Email(email);
        }
        catch
        {
            return;
        }

        var user = await userRepository.GetByEmailAsync(emailVO, ct);
        if (user is null)
            return;

        await tokenRepository.DeleteByUserIdAsync(user.Id, ct);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenString = Base64UrlEncode(tokenBytes);
        var resetToken = new PasswordResetToken(tokenString, DateTimeOffset.UtcNow.AddMinutes(5));

        await tokenRepository.SaveAsync(user.Id, resetToken, ct);
        await eventDispatcher.DispatchAsync(
            [new PasswordResetRequested(user.Id, emailVO, tokenString)],
            ct);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
