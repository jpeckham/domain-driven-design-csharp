using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class ReplyComposerSourceTests
{
    [Fact]
    public void FeedReplyAction_NavigatesToPostDetailInsteadOfShowingInlineComposer()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "Feed.razor"));

        source.Should().Contain("private void OpenReply(Guid postId)");
        source.Should().Contain("Nav.NavigateTo($\"/posts/{postId}\")");
        source.Should().NotContain("_replyingTo == post.PostId");
        source.Should().NotContain("SubmitReplyAsync");
        source.Should().NotContain("CreateReplyAsync(_replyingTo.Value");
    }

    [Fact]
    public void FeedPage_DoesNotOwnReplyComposerState()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SocialDDD.Client",
            "Pages",
            "Feed.razor"));

        source.Should().NotContain("private Guid? _replyingTo");
        source.Should().NotContain("private string _replyContent");
        source.Should().NotContain("_replyMediaAssetIds");
        source.Should().NotContain("ReplyContentMaxLength");
        source.Should().NotContain("StoredReplyLength");
        source.Should().NotContain("reply-target");
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
