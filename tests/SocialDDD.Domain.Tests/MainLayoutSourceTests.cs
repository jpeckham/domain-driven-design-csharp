using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class MainLayoutSourceTests
{
    [Fact]
    public void MainLayout_RendersAuthenticatedAndAnonymousNavLinks()
    {
        var source = ReadMainLayout();

        source.Should().Contain("<a href=\"/login\">Log in</a>");
        source.Should().Contain("<a href=\"/register\">Register</a>");
        source.Should().Contain("<a href=\"@($\"/profile/{_currentHandle.TrimStart('@')}\")\">Profile</a>");
        source.Should().Contain("Log out");
    }

    [Fact]
    public void MainLayout_RefreshesAuthStateWhenNavigationChanges()
    {
        var source = ReadMainLayout();

        source.Should().Contain("@implements IDisposable");
        source.Should().Contain("Nav.LocationChanged +=");
        source.Should().Contain("Nav.LocationChanged -=");
        source.Should().Contain("private async Task RefreshAuthStateAsync()");
        source.Should().Contain("await RefreshAuthStateAsync();");
        source.Should().Contain("InvokeAsync(RefreshAuthStateAsync)");
    }

    private static string ReadMainLayout()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialDDD.sln")))
            dir = dir.Parent;

        var root = dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        return File.ReadAllText(Path.Combine(root, "src", "SocialDDD.Client", "Layout", "MainLayout.razor"));
    }
}
