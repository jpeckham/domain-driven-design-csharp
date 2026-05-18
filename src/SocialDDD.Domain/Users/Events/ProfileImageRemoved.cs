using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record ProfileImageRemoved(UserId UserId) : IDomainEvent;
