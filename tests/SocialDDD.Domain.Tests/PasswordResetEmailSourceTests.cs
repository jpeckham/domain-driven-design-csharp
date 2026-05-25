using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public sealed class PasswordResetEmailSourceTests
{
    [Fact]
    public void ConsoleEmailService_LogsBrowserReadyResetLink()
    {
        var source = ReadRepositoryFile("src", "SocialDDD.Infrastructure", "Identity", "Emails", "ConsoleEmailService.cs");

        source.Should().Contain("Client:BaseUrl");
        source.Should().Contain("reset-password?token=");
        source.Should().Contain("Uri.EscapeDataString(token)");
        source.Should().Contain("reset link = {ResetLink}");
    }

    [Fact]
    public void DockerCompose_ConfiguresPublicClientBaseUrlForApi()
    {
        var source = ReadRepositoryFile("docker-compose.yml");

        source.Should().Contain("Client__BaseUrl: http://localhost:5200");
    }

    [Fact]
    public void ApiCors_UsesConfiguredClientBaseUrl()
    {
        var source = ReadRepositoryFile("src", "SocialDDD.Api", "Program.cs");

        source.Should().Contain("builder.Configuration[\"Client:BaseUrl\"]");
        source.Should().Contain("configuredClientBaseUrl");
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialDDD.sln")))
            dir = dir.Parent;

        var root = dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(pathParts).ToArray()));
    }
}
