using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record PasswordReset(UserId UserId) : IDomainEvent;
