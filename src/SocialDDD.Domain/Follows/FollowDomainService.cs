using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Follows;

public sealed class FollowDomainService(IBlockRepository blockRepository)
{
    public async Task EnsureCanFollowAsync(Handle follower, Handle followed, CancellationToken ct = default)
    {
        if (await blockRepository.IsBlockedAsync(follower, followed, ct)
            || await blockRepository.IsBlockedAsync(followed, follower, ct))
            throw new DomainException("Cannot follow a user when either user has blocked the other.");
    }
}
