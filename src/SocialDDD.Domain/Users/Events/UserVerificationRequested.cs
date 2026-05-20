using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserVerificationRequested(UserId UserId, Email Email, string Code) : IDomainEvent;
