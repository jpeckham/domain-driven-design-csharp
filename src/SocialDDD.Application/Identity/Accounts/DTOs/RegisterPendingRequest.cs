namespace SocialDDD.Application.Identity.Accounts.DTOs;

public sealed record RegisterPendingRequest(
    string Username,
    string Email,
    string Password,
    string Handle,
    string DisplayName);
