namespace SocialDDD.Application.Identity.Accounts.DTOs;

public sealed record LoginWithDeviceRequest(string Email, string Password, string DeviceId);
