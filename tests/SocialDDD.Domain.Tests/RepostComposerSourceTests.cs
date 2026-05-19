using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class RepostComposerSourceTests
{
    [Fact]
    public void FeedRepostAction_OpensComposerWithoutConfirmingEmptyRepost()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "Feed.razor"));

        source.Should().Contain("_reposting = postId;");
        source.Should().NotContain("ConfirmAsync(\"Repost without commentary?\")");
        source.Should().NotContain("CreateRepostAsync(postId)");
    }

    [Fact]
    public void FeedRepostComposer_SubmitsRepostWithOptionalCommentary()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "Feed.razor"));

        source.Should().Contain("@onsubmit=\"SubmitQuoteRepostAsync\"");
        source.Should().Contain("placeholder=\"Add a comment\"");
        source.Should().Contain("CreateRepostAsync(_reposting.Value, _repostCommentary)");
        source.Should().Contain("<button type=\"submit\" disabled=\"@_busy\">Repost</button>");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialDDD.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
