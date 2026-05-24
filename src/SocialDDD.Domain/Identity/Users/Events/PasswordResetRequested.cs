using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Identity.Users.Events;

public sealed record PasswordResetRequested(UserId UserId, Email Email, string Token) : IDomainEvent;
