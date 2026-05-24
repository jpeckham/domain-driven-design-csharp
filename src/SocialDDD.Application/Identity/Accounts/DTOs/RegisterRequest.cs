namespace SocialDDD.Application.Identity.Accounts.DTOs;

public sealed record RegisterRequest(string Username, string Email, string Password, string Handle, string DisplayName);
