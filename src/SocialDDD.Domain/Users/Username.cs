using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record Username
{
    public string Value { get; }

    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 50)
            throw new DomainValidationException("Username must be 1–50 characters.");
        Value = value.Trim();
    }
}
