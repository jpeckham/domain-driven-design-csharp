using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserRegistered(UserId UserId, Email Email, Handle Handle, DisplayName DisplayName) : IDomainEvent;
