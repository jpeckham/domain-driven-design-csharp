using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Social.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Social.Follows;

public sealed class FollowDomainService(IBlockRepository blockRepository)
{
    public async Task EnsureCanFollowAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        if (await blockRepository.IsBlockedAsync(follower, followed, ct)
            || await blockRepository.IsBlockedAsync(followed, follower, ct))
            throw new DomainException("Cannot follow a user when either user has blocked the other.");
    }
}
