using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var username = new Username(request.Username);

        if (await userRepository.ExistsByEmailAsync(email, ct))
            throw new DomainException("Email is already registered.");

        if (await userRepository.ExistsByUsernameAsync(username, ct))
            throw new DomainException("Username is already taken.");

        var hash = passwordHasher.Hash(request.Password);
        var user = User.Register(username, email, new PasswordHash(hash), new Handle(request.Username), new DisplayName(request.Username));

        await userRepository.AddAsync(user, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainException("Invalid credentials.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash.Value))
            throw new DomainException("Invalid credentials.");

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(id), ct)
            ?? throw new DomainException($"User {id} not found.");

        return new UserDto(user.Id.Value, user.Username.Value, user.Email.Value, user.RegisteredAt);
    }
}
