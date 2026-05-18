using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record ProfileImageUpdated(UserId UserId, Guid AssetId) : IDomainEvent;
