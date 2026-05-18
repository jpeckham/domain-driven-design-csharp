using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Posts.Events;

public sealed record PostUnliked(PostId PostId, Handle UnlikedByHandle) : IDomainEvent;
