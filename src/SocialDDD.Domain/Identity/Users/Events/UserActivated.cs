using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Identity.Users.Events;

public sealed record UserActivated(UserId UserId) : IDomainEvent;
