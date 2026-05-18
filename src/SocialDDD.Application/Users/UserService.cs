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
        var handle = new Handle(request.Handle);
        var displayName = new DisplayName(request.DisplayName);

        if (await userRepository.ExistsByEmailAsync(email, ct))
            throw new DomainException("Email is already registered.");

        if (await userRepository.ExistsByUsernameAsync(username, ct))
            throw new DomainException("Username is already taken.");

        if (await userRepository.HandleExistsAsync(handle, ct))
            throw new DomainException("Handle is already taken.");

        var hash = passwordHasher.Hash(request.Password);
        var user = User.RegisterImmediate(username, email, new PasswordHash(hash), handle, displayName);

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

        if (user.Status == UserStatus.Pending)
            throw new DomainException("Account is not yet verified. Please check your email.");

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(id), ct)
            ?? throw new DomainException($"User {id} not found.");

        return ToDto(user);
    }

    public async Task<UserDto> GetByHandleAsync(string rawHandle, CancellationToken ct = default)
    {
        var handle = new Handle(rawHandle);
        var user = await userRepository.FindByHandleAsync(handle, ct)
            ?? throw new DomainException($"User with handle @{handle.Value} not found.");

        return ToDto(user);
    }

    public async Task UpdateDisplayNameAsync(Guid id, UpdateDisplayNameRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(id), ct)
            ?? throw new DomainException($"User {id} not found.");

        user.UpdateDisplayName(new DisplayName(request.DisplayName));
        await userRepository.UpdateAsync(user, ct);
    }

    private static UserDto ToDto(User user) =>
        new(user.Id.Value, user.Username.Value, user.Email.Value, user.Handle.Display, user.DisplayName.Value, user.RegisteredAt);
}
