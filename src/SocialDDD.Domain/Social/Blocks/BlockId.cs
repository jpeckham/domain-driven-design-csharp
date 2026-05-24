using SocialDDD.Domain.Social.Profiles;
namespace SocialDDD.Domain.Social.Blocks;

public sealed record BlockId(Guid Value)
{
    public static BlockId New() => new(Guid.NewGuid());
    public static BlockId From(Guid value) => new(value);
}
