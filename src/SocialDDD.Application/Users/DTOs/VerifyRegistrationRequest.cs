namespace SocialDDD.Application.Users.DTOs;

public sealed record VerifyRegistrationRequest(string Email, string Code);
