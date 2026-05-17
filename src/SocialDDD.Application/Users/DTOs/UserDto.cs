namespace SocialDDD.Application.Users.DTOs;

public sealed record UserDto(Guid UserId, string Username, string Email, DateTime RegisteredAt);
