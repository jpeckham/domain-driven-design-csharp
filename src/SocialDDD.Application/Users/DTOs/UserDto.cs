namespace SocialDDD.Application.Users.DTOs;

public sealed record UserDto(Guid UserId, string Username, string Email, string Handle, string DisplayName, DateTime RegisteredAt);
