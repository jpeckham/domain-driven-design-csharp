namespace SocialDDD.Application.Identity.Accounts.DTOs;

public sealed record TokenResponse(string Token, Guid UserId, string Username);
