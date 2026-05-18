using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class VerifyDeviceOtpCommand(
    IUserRepository userRepository,
    IOtpRepository otpRepository,
    IRememberedDeviceRepository rememberedDeviceRepository,
    ITokenService tokenService)
{
    public async Task<TokenResponse> ExecuteAsync(VerifyDeviceOtpRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var deviceId = new DeviceId(request.DeviceId);

        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainValidationException("Invalid credentials.");

        var otp = await otpRepository.FindAsync(user.Id, deviceId, ct)
            ?? throw new DomainValidationException("No OTP found for this device. Please log in again.");

        if (otp.IsExpired(DateTimeOffset.UtcNow))
        {
            await otpRepository.DeleteAsync(user.Id, deviceId, ct);
            throw new DomainValidationException("OTP has expired. Please log in again.");
        }

        var storedBytes = System.Text.Encoding.UTF8.GetBytes(otp.Code);
        var providedBytes = System.Text.Encoding.UTF8.GetBytes(request.Otp);
        if (!CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes))
            throw new DomainValidationException("Invalid OTP.");

        await otpRepository.DeleteAsync(user.Id, deviceId, ct);

        if (request.RememberDevice)
            await rememberedDeviceRepository.RememberAsync(user.Id, deviceId, ct);

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }
}
