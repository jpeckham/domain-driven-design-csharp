using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserRegistered(UserId UserId) : IDomainEvent;
