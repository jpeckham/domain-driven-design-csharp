using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Auth;

internal sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string plaintext) =>
        BCrypt.Net.BCrypt.HashPassword(plaintext);

    public bool Verify(string plaintext, string hash) =>
        BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
