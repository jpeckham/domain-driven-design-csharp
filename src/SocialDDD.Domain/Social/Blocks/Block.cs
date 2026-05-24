using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Domain.Social.Blocks;

public sealed class Block : AggregateRoot<BlockId>
{
    public Handle BlockerHandle { get; private set; } = null!;
    public Handle BlockedHandle { get; private set; } = null!;
    public DateTime BlockedAt { get; private set; }

    private Block() { }

    public static Block Create(Handle blocker, Handle blocked)
    {
        if (blocker == blocked)
            throw new DomainException("Users cannot block themselves.");

        var block = new Block
        {
            Id = BlockId.New(),
            BlockerHandle = blocker,
            BlockedHandle = blocked,
            BlockedAt = DateTime.UtcNow
        };

        return block;
    }
}
