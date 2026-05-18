namespace SocialDDD.Application.Users.DTOs;

public sealed record LoginWithDeviceRequest(string Email, string Password, string DeviceId);
