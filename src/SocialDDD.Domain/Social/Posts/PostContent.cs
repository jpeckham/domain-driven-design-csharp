using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Social.Posts;

public sealed record PostContent
{
    public string Value { get; }

    public PostContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 280)
            throw new DomainValidationException("Post content must be 1–280 characters.");
        Value = value;
    }
}
