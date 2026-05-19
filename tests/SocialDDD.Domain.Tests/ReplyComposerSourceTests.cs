using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class ReplyComposerSourceTests
{
    [Fact]
    public void FeedReplyComposer_DoesNotPrefillEditableTextWithParentHandle()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "Feed.razor"));

        source.Should().Contain("Replying to @_replyTargetHandle");
        source.Should().NotContain("_replyContent = post?.AuthorHandle");
    }

    [Fact]
    public void FeedReplySubmission_IncrementsParentReplyCountOptimistically()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "Feed.razor"));

        source.Should().Contain("ReplyCount = parent.ReplyCount + 1");
    }

    [Fact]
    public void PostDetailReplyComposer_DoesNotPrefillEditableTextWithParentHandle()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "PostDetail.razor"));

        source.Should().Contain("Replying to @ReplyTargetHandle");
        source.Should().NotContain("_replyContent = _conversation?.Post.AuthorHandle");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialDDD.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
