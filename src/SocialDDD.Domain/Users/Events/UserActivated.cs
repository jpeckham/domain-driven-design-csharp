using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserActivated(UserId UserId) : IDomainEvent;
