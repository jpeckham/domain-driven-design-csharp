using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace SocialDDD.Client.Services;

public sealed class AuthService(HttpClient http, IJSRuntime js)
{
    private const string TokenKey = "auth_token";
    private const string UserIdKey = "user_id";
    private const string UsernameKey = "username";

    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/users/register", new { username, email, password });
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<TokenResult>();
        if (result is null) return false;

        await StoreTokenAsync(result);
        return true;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("api/users/login", new { email, password });
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<TokenResult>();
        if (result is null) return false;

        await StoreTokenAsync(result);
        return true;
    }

    public async Task LogoutAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
        await js.InvokeVoidAsync("localStorage.removeItem", UsernameKey);
        http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<bool> InitializeAsync()
    {
        var token = await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (string.IsNullOrEmpty(token)) return false;

        SetAuthHeader(token);
        return true;
    }

    public async Task<Guid?> GetCurrentUserIdAsync()
    {
        var id = await js.InvokeAsync<string?>("localStorage.getItem", UserIdKey);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    public async Task<string?> GetCurrentUsernameAsync() =>
        await js.InvokeAsync<string?>("localStorage.getItem", UsernameKey);

    private async Task StoreTokenAsync(TokenResult result)
    {
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.Token);
        await js.InvokeVoidAsync("localStorage.setItem", UserIdKey, result.UserId.ToString());
        await js.InvokeVoidAsync("localStorage.setItem", UsernameKey, result.Username);
        SetAuthHeader(result.Token);
    }

    private void SetAuthHeader(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private sealed record TokenResult(string Token, Guid UserId, string Username);
}
