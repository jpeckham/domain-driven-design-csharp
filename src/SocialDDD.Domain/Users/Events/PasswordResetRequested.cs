using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record PasswordResetRequested(UserId UserId, Email Email, string Token) : IDomainEvent;
