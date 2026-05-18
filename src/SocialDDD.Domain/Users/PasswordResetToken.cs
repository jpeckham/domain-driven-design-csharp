using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record PasswordResetToken
{
    public string Token { get; }
    public DateTimeOffset ExpiresAt { get; }

    public PasswordResetToken(string token, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(token))
            throw new DomainValidationException("Password reset token must not be empty.");
        Token = token;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
