namespace SocialDDD.Application.Users.DTOs;

public sealed record RegisterRequest(string Username, string Email, string Password);
