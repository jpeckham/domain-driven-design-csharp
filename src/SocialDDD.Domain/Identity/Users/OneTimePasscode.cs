using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Identity.Users;

public sealed record OneTimePasscode
{
    public string Code { get; }
    public DateTimeOffset ExpiresAt { get; }

    public OneTimePasscode(string code, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(code))
            throw new DomainValidationException("OTP code must not be empty.");
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new DomainValidationException("OTP code must be exactly 6 digits.");
        Code = code;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
