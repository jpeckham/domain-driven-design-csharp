namespace SocialDDD.Client.Services;

public sealed class ToastService
{
    public event Action<string, string>? OnShow;

    public void Success(string message) => OnShow?.Invoke(message, "success");
    public void Error(string message) => OnShow?.Invoke(message, "error");
}
