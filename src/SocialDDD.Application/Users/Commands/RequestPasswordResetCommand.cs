using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class RequestPasswordResetCommand(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IEmailService emailService)
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
            // Silently succeed — no information leak
            return;
        }

        var user = await userRepository.GetByEmailAsync(emailVO, ct);
        if (user is null)
            return; // Silently succeed — no information leak

        // Delete any existing reset token for this user
        await tokenRepository.DeleteByUserIdAsync(user.Id, ct);

        // Generate a cryptographically random 32-byte Base64Url token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenString = Base64UrlEncode(tokenBytes);
        var resetToken = new PasswordResetToken(tokenString, DateTimeOffset.UtcNow.AddMinutes(5));

        await tokenRepository.SaveAsync(user.Id, resetToken, ct);
        await emailService.SendPasswordResetEmailAsync(emailVO.Value, tokenString, ct);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
