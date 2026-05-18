using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts.Events;

public sealed record PostReposted(PostId OriginalPostId, Handle ReposterHandle) : IDomainEvent;
