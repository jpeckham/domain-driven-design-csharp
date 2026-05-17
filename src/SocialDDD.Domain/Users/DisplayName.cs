using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record DisplayName
{
    public string Value { get; }

    public DisplayName(string value)
    {
        if (value is null)
            throw new DomainException("DisplayName must be 1-50 characters.");
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 50)
            throw new DomainException("DisplayName must be 1-50 characters.");
        Value = trimmed;
    }
}
