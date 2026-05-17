using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserRegistered(UserId UserId, Handle Handle, DisplayName DisplayName) : IDomainEvent;
