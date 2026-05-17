using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}
