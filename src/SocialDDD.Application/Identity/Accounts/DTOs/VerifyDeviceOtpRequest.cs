namespace SocialDDD.Application.Identity.Accounts.DTOs;

public sealed record VerifyDeviceOtpRequest(string Email, string DeviceId, string Otp, bool RememberDevice);
