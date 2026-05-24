using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Identity.Users.Events;

public sealed record UserRegistered(UserId UserId, Email Email, Handle Handle, DisplayName DisplayName) : IDomainEvent;
