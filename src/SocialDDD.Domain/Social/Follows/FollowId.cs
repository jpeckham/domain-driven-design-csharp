using SocialDDD.Domain.Social.Profiles;
namespace SocialDDD.Domain.Social.Follows;

public sealed record FollowId(Guid Value)
{
    public static FollowId New() => new(Guid.NewGuid());
    public static FollowId From(Guid value) => new(value);
}
