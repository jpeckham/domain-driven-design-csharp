namespace SocialDDD.Application.Identity.Accounts.DTOs;

public sealed record VerifyRegistrationRequest(string Email, string Code);
