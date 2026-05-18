using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace SocialDDD.Client.Services;

public sealed class AuthService(HttpClient http, IJSRuntime js)
{
    private const string TokenKey = "auth_token";
    private const string UserIdKey = "user_id";
    private const string UsernameKey = "username";
    private const string HandleKey = "handle";
    private const string DeviceIdKey = "device_id";

    public async Task<string?> RegisterPendingAsync(string handle, string displayName, string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/registrations", new { username = handle, email, password, handle, displayName });
        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorAsync(response, "Registration failed.");
        }

        return null;
    }

    public async Task<string?> ResendRegistrationCodeAsync(string email)
    {
        var response = await http.PostAsJsonAsync("api/registrations", new
        {
            username = "",
            email,
            password = "",
            handle = "",
            displayName = ""
        });
        return response.IsSuccessStatusCode
            ? null
            : await ReadErrorAsync(response, "Unable to resend the code.");
    }

    public async Task<string?> VerifyRegistrationAsync(string email, string code)
    {
        var response = await http.PostAsJsonAsync("api/registrations/verify", new { email, code });
        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorAsync(response, "Verification failed.");
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResult>();
        if (result is null) return "Verification failed.";

        await StoreTokenAsync(result);
        return null;
    }

    public async Task<LoginResult> LoginWithDeviceAsync(string email, string password, string deviceId)
    {
        var response = await http.PostAsJsonAsync("api/sessions/device", new { email, password, deviceId });
        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            return LoginResult.OtpRequired();
        }

        if (!response.IsSuccessStatusCode)
        {
            return LoginResult.Failed(await ReadErrorAsync(response, "Invalid email or password."));
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResult>();
        if (result is null) return LoginResult.Failed("Invalid email or password.");

        await StoreTokenAsync(result);
        return LoginResult.Success();
    }

    public async Task<string?> VerifyDeviceOtpAsync(string email, string deviceId, string otp, bool rememberDevice)
    {
        var response = await http.PostAsJsonAsync("api/sessions/device/verify", new { email, deviceId, otp, rememberDevice });
        if (!response.IsSuccessStatusCode)
        {
            return await ReadErrorAsync(response, "Verification failed.");
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResult>();
        if (result is null) return "Verification failed.";

        await StoreTokenAsync(result);
        return null;
    }

    public async Task<string?> RequestPasswordResetAsync(string email)
    {
        var response = await http.PostAsJsonAsync("api/password-reset-requests", new { email });
        return response.IsSuccessStatusCode
            ? null
            : await ReadErrorAsync(response, "Password reset request failed.");
    }

    public async Task<string?> ResetPasswordAsync(string token, string newPassword)
    {
        var response = await http.PostAsJsonAsync("api/password-resets", new { token, newPassword });
        return response.IsSuccessStatusCode
            ? null
            : await ReadErrorAsync(response, "Password reset failed.");
    }

    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        var deviceId = await js.InvokeAsync<string?>("localStorage.getItem", DeviceIdKey);
        if (!string.IsNullOrWhiteSpace(deviceId)) return deviceId;

        deviceId = Guid.NewGuid().ToString("N");
        await js.InvokeVoidAsync("localStorage.setItem", DeviceIdKey, deviceId);
        return deviceId;
    }

    public async Task LogoutAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UsernameKey);
        await js.InvokeVoidAsync("localStorage.removeItem", HandleKey);
        http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<bool> InitializeAsync()
    {
        var token = await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (string.IsNullOrEmpty(token)) return false;

        SetAuthHeader(token);

        var idStr = await js.InvokeAsync<string?>("localStorage.getItem", UserIdKey);
        if (!Guid.TryParse(idStr, out var userId)) { await LogoutAsync(); return false; }

        var response = await http.GetAsync("api/users/me");
        if (!response.IsSuccessStatusCode) { await LogoutAsync(); return false; }

        var user = await response.Content.ReadFromJsonAsync<UserResult>();
        if (!string.IsNullOrWhiteSpace(user?.Handle))
            await js.InvokeVoidAsync("localStorage.setItem", HandleKey, user.Handle);

        return true;
    }

    public async Task<UserResult?> GetCurrentUserAsync()
    {
        if (!await InitializeAsync()) return null;
        return await http.GetFromJsonAsync<UserResult>("api/users/me");
    }

    public async Task<Guid?> GetCurrentUserIdAsync()
    {
        var id = await js.InvokeAsync<string?>("localStorage.getItem", UserIdKey);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    public async Task<string?> GetCurrentUsernameAsync() =>
        await js.InvokeAsync<string?>("localStorage.getItem", UsernameKey);

    public async Task<string?> GetCurrentHandleAsync()
    {
        var handle = await js.InvokeAsync<string?>("localStorage.getItem", HandleKey);
        if (!string.IsNullOrWhiteSpace(handle)) return handle;

        if (!await InitializeAsync()) return null;
        return await js.InvokeAsync<string?>("localStorage.getItem", HandleKey);
    }

    private async Task StoreTokenAsync(TokenResult result)
    {
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.Token);
        await js.InvokeVoidAsync("localStorage.setItem", UserIdKey, result.UserId.ToString());
        await js.InvokeVoidAsync("localStorage.setItem", UsernameKey, result.Username);
        await js.InvokeVoidAsync("localStorage.setItem", HandleKey, result.Username.StartsWith('@') ? result.Username : $"@{result.Username}");
        SetAuthHeader(result.Token);
    }

    private void SetAuthHeader(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
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

    private sealed record TokenResult(string Token, Guid UserId, string Username);
    public sealed record UserResult(Guid UserId, string Username, string Handle, string DisplayName, string? ProfileImageUrl);
    private sealed record ErrorResult(string Error);

    public sealed record LoginResult(bool IsSuccess, bool RequiresOtp, string? Error)
    {
        public static LoginResult Success() => new(true, false, null);
        public static LoginResult OtpRequired() => new(false, true, null);
        public static LoginResult Failed(string error) => new(false, false, error);
    }
}
