using SocialDDD.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace SocialDDD.Domain.Social.Profiles;

public sealed partial record Handle
{
    [GeneratedRegex(@"^[a-z0-9_]{1,30}$")]
    private static partial Regex ValidPattern();

    public string Value { get; }
    public string Display => "@" + Value;

    public Handle(string value)
    {
        if (value is null)
            throw new DomainValidationException("Handle must be 1-30 characters: letters, digits, and underscores only.");
        var normalized = value.ToLowerInvariant();
        if (!ValidPattern().IsMatch(normalized))
            throw new DomainValidationException("Handle must be 1-30 characters: letters, digits, and underscores only.");
        Value = normalized;
    }
}
