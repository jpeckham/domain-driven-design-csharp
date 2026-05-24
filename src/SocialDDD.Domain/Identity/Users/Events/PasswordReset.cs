using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Identity.Users.Events;

public sealed record PasswordReset(UserId UserId) : IDomainEvent;
