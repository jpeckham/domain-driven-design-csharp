using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Social.Follows;

public sealed class Follow : AggregateRoot<FollowId>
{
    public Handle FollowerHandle { get; private set; } = null!;
    public Handle FollowedHandle { get; private set; } = null!;
    public DateTime FollowedAt { get; private set; }

    private Follow() { }

    public static Follow Create(Handle follower, Handle followed)
    {
        if (follower == followed)
            throw new DomainException("Users cannot follow themselves.");

        return new Follow
        {
            Id = FollowId.New(),
            FollowerHandle = follower,
            FollowedHandle = followed,
            FollowedAt = DateTime.UtcNow
        };
    }
}
