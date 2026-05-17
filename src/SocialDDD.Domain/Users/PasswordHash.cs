namespace SocialDDD.Domain.Users;

// Opaque wrapper — the domain stores the hash but never computes it.
public sealed record PasswordHash(string Value);
