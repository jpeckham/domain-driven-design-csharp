using SocialDDD.Domain.Identity.Users;
using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Social.Profiles.Events;

public sealed record ProfileImageRemoved(UserId UserId) : IDomainEvent;
