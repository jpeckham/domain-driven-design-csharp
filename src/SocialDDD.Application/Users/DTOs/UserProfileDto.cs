namespace SocialDDD.Application.Users.DTOs;

public sealed record UserProfileDto(
    Guid UserId,
    string Username,
    string Handle,
    string DisplayName,
    DateTime RegisteredAt,
    string? ProfileImageUrl,
    int FollowerCount,
    int FollowingCount,
    bool IsOwnProfile,
    bool IsFollowedByMe,
    bool IsBlockedByMe);
