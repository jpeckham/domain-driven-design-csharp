using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record VerificationCode
{
    public string Code { get; }
    public DateTimeOffset ExpiresAt { get; }

    public VerificationCode(string code, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(code))
            throw new DomainValidationException("Verification code must not be empty.");
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new DomainValidationException("Verification code must be exactly 6 digits.");
        Code = code;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
