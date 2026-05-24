using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Identity.Users;

public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        var normalised = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalised.Length == 0 || normalised.Length > 320 || !normalised.Contains('@'))
            throw new DomainValidationException("Invalid email address.");
        Value = normalised;
    }
}
