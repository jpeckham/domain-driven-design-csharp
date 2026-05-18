using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Blocks;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Follows;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDomainEventDispatcher eventDispatcher,
    IFollowRepository followRepository,
    IBlockRepository blockRepository)
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

    public async Task<UserProfileDto> GetProfileByHandleAsync(
        string rawHandle, Guid? requesterId = null, CancellationToken ct = default)
    {
        var handle = new Handle(rawHandle);
        var user = await userRepository.FindByHandleAsync(handle, ct)
            ?? throw new DomainException($"User with handle @{handle.Value} not found.");

        var requester = requesterId is not null
            ? await userRepository.GetByIdAsync(UserId.From(requesterId.Value), ct)
            : null;

        var isOwnProfile = requester?.Id == user.Id;
        var isFollowedByMe = requester is not null && !isOwnProfile
            && await followRepository.IsFollowingAsync(requester.Handle, user.Handle, ct);
        var isBlockedByMe = requester is not null && !isOwnProfile
            && await blockRepository.IsBlockedAsync(requester.Handle, user.Handle, ct);

        return new UserProfileDto(
            user.Id.Value,
            user.Username.Value,
            user.Handle.Display,
            user.DisplayName.Value,
            user.RegisteredAt,
            ProfileImageUrl(user),
            await followRepository.CountFollowersAsync(user.Handle, ct),
            await followRepository.CountFollowingAsync(user.Handle, ct),
            isOwnProfile,
            isFollowedByMe,
            isBlockedByMe);
    }

    public async Task UpdateDisplayNameAsync(Guid id, UpdateDisplayNameRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(id), ct)
            ?? throw new DomainException($"User {id} not found.");

        user.UpdateDisplayName(new DisplayName(request.DisplayName));
        await userRepository.UpdateAsync(user, ct);
    }

    private static UserDto ToDto(User user) =>
        new(user.Id.Value, user.Username.Value, user.Email.Value, user.Handle.Display, user.DisplayName.Value, user.RegisteredAt, ProfileImageUrl(user));

    private static string? ProfileImageUrl(User user) =>
        user.ProfileImage is null ? null : $"/api/profile-images/{user.ProfileImage.AssetId}";
}
