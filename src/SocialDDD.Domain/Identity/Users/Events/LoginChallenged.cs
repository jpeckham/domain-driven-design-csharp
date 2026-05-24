using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Identity.Users.Events;

public sealed record LoginChallenged(UserId UserId, Email Email, DeviceId DeviceId, string Otp) : IDomainEvent;
