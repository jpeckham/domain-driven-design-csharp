using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record DeviceId
{
    public string Value { get; }

    public DeviceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException("DeviceId must not be null or empty.");
        if (!Guid.TryParse(value, out _))
            throw new DomainValidationException("DeviceId must be a valid GUID.");
        Value = value.ToLowerInvariant();
    }

    public static DeviceId New() => new(Guid.NewGuid().ToString());
}
