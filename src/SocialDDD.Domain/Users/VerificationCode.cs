namespace SocialDDD.Domain.Users;

public sealed record VerificationCode(string Code, DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
