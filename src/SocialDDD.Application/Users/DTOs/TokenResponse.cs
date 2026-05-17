namespace SocialDDD.Application.Users.DTOs;

public sealed record TokenResponse(string Token, Guid UserId, string Username);
