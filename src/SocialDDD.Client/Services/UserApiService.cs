using System.Net.Http.Json;
using System.Text.Json;

namespace SocialDDD.Client.Services;

public sealed class UserApiService(HttpClient http)
{
    public async Task<UserProfileDto?> GetUserProfileAsync(string handle)
    {
        var normalized = handle.TrimStart('@');
        return await http.GetFromJsonAsync<UserProfileDto>(
            $"api/users/by-handle/{Uri.EscapeDataString(normalized)}");
    }

    public async Task<bool> FollowUserAsync(string handle) =>
        (await http.PostAsync($"api/users/{Uri.EscapeDataString(handle.TrimStart('@'))}/follows", null)).IsSuccessStatusCode;

    public async Task<bool> UnfollowUserAsync(string handle) =>
        (await http.DeleteAsync($"api/users/{Uri.EscapeDataString(handle.TrimStart('@'))}/follows")).IsSuccessStatusCode;

    public async Task<bool> BlockUserAsync(string handle) =>
        (await http.PostAsync($"api/users/{Uri.EscapeDataString(handle.TrimStart('@'))}/blocks", null)).IsSuccessStatusCode;

    public async Task<bool> UnblockUserAsync(string handle) =>
        (await http.DeleteAsync($"api/users/{Uri.EscapeDataString(handle.TrimStart('@'))}/blocks")).IsSuccessStatusCode;

    public Task<bool> FollowAsync(string handle) => FollowUserAsync(handle);
    public Task<bool> UnfollowAsync(string handle) => UnfollowUserAsync(handle);
    public Task<bool> BlockAsync(string handle) => BlockUserAsync(handle);
    public Task<bool> UnblockAsync(string handle) => UnblockUserAsync(handle);

    public async Task<bool> UpdateDisplayNameAsync(string displayName) =>
        (await http.PutAsJsonAsync("api/users/me/display-name", new { displayName })).IsSuccessStatusCode;

    public async Task<BeginProfileImageUploadResult?> BeginProfileImageUploadAsync(string contentType)
    {
        var response = await http.PostAsJsonAsync("api/users/me/profile-image/upload-sessions", new { contentType });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BeginProfileImageUploadResult>()
            : null;
    }

    public async Task<bool> PutProfileImageBytesAsync(string uploadUrl, Stream stream, string contentType)
    {
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return (await http.PutAsync(uploadUrl.TrimStart('/'), content)).IsSuccessStatusCode;
    }

    public Task<bool> PutProfileImageAsync(string uploadUrl, Stream stream, string contentType) =>
        PutProfileImageBytesAsync(uploadUrl, stream, contentType);

    public async Task<bool> CompleteProfileImageUploadAsync(
        Guid assetId, string contentType, long byteLength, int? width = null, int? height = null) =>
        (await http.PostAsJsonAsync("api/users/me/profile-image/complete", new
        {
            assetId,
            contentType,
            byteLength,
            width,
            height
        })).IsSuccessStatusCode;

    public async Task<bool> RemoveProfileImageAsync() =>
        (await http.DeleteAsync("api/users/me/profile-image")).IsSuccessStatusCode;

    public static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var err = await response.Content.ReadFromJsonAsync<ErrorResult>();
            return string.IsNullOrWhiteSpace(err?.Error) ? fallback : err.Error;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

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

    public sealed record BeginProfileImageUploadResult(Guid AssetId, string UploadUrl);
    private sealed record ErrorResult(string Error);
}
