using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Application.Users.Commands;

public abstract class LoginWithDeviceResult
{
    private LoginWithDeviceResult() { }

    public sealed class Success(string token, Guid userId, string username) : LoginWithDeviceResult
    {
        public string Token { get; } = token;
        public Guid UserId { get; } = userId;
        public string Username { get; } = username;
    }

    public sealed class OtpRequired : LoginWithDeviceResult { }
}

public sealed class LoginWithDeviceCommand(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IRememberedDeviceRepository rememberedDeviceRepository,
    IOtpRepository otpRepository,
    IDomainEventDispatcher eventDispatcher,
    ITokenService tokenService)
{
    public async Task<LoginWithDeviceResult> ExecuteAsync(LoginWithDeviceRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var deviceId = new DeviceId(request.DeviceId);

        var user = await userRepository.GetByEmailAsync(email, ct);
        if (user is null)
            throw new DomainValidationException("Invalid credentials.");

        if (user.Status == UserStatus.Pending)
            throw new DomainValidationException("Account is not yet verified. Please check your email.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash.Value))
            throw new DomainValidationException("Invalid credentials.");

        var isKnown = await rememberedDeviceRepository.IsRememberedAsync(user.Id, deviceId, ct);
        if (isKnown)
            return new LoginWithDeviceResult.Success(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);

        var otp = new OneTimePasscode(GenerateOtp(), DateTimeOffset.UtcNow.AddMinutes(10));
        await otpRepository.SaveAsync(user.Id, deviceId, otp, ct);
        await eventDispatcher.DispatchAsync(
            [new LoginChallenged(user.Id, user.Email, deviceId, otp.Code)],
            ct);

        return new LoginWithDeviceResult.OtpRequired();
    }

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
