using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class FeedChromeSourceTests
{
    [Fact]
    public void Feed_DoesNotRenderDuplicateLogoutControl()
    {
        var source = ReadFeedSource();

        source.Should().NotContain("Log Out");
        source.Should().NotContain("@onclick=\"Logout\"");
        source.Should().NotContain("private async Task Logout()");
    }

    [Fact]
    public void Feed_DoesNotRenderBlockUserControls()
    {
        var source = ReadFeedSource();

        source.Should().NotContain("Block @post.AuthorHandle");
        source.Should().NotContain("BlockFromFeedAsync");
        source.Should().NotContain("UserApiService UserApi");
    }

    private static string ReadFeedSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialDDD.sln")))
            dir = dir.Parent;

        var root = dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        return File.ReadAllText(Path.Combine(root, "src", "SocialDDD.Client", "Pages", "Feed.razor"));
    }
}
