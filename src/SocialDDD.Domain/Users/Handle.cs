using SocialDDD.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace SocialDDD.Domain.Users;

public sealed record Handle
{
    private static readonly Regex ValidPattern = new(@"^[a-z0-9_]{1,30}$", RegexOptions.Compiled);

    public string Value { get; }
    public string Display => "@" + Value;

    public Handle(string value)
    {
        var normalized = value?.ToLowerInvariant() ?? string.Empty;
        if (!ValidPattern.IsMatch(normalized))
            throw new DomainException("Handle must be 1–30 characters: letters, digits, and underscores only.");
        Value = normalized;
    }
}
