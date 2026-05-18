namespace SocialDDD.Application.Users.DTOs;

public sealed record VerifyDeviceOtpRequest(string Email, string DeviceId, string Otp, bool RememberDevice);
