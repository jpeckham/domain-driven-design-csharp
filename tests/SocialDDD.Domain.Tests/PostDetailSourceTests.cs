using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class PostDetailSourceTests
{
    [Fact]
    public void ConversationDtos_ExposeFocusedParentPost()
    {
        var applicationDto = ReadRepositoryFile("src", "SocialDDD.Application", "Social", "Posts", "DTOs", "PostConversationDto.cs");
        var clientService = ReadRepositoryFile("src", "SocialDDD.Client", "Services", "PostApiService.cs");

        applicationDto.Should().Contain("PostDto? ParentPost");
        clientService.Should().Contain("PostDto? ParentPost");
    }

    [Fact]
    public void ConversationQuery_PopulatesParentPost_ForFocusedReplies()
    {
        var source = ReadRepositoryFile(
            "src",
            "SocialDDD.Application",
            "Social",
            "Posts",
            "Queries",
            "GetPostWithConversationQuery.cs");

        source.Should().Contain("ParentPostId is not null");
        source.Should().Contain("parentPost");
        source.Should().Contain("await ToDtoAsync(parentPost)");
    }

    [Fact]
    public void PostDetail_RendersParentQuote_WhenFocusedPostIsReply()
    {
        var source = ReadRepositoryFile("src", "SocialDDD.Client", "Pages", "PostDetail.razor");

        source.Should().Contain("_conversation.ParentPost is not null");
        source.Should().Contain("reply-context");
        source.Should().Contain("<AuthorThumbnail DisplayName=\"@ParentAuthorName\"");
        source.Should().Contain("<PostContent Content=\"@_conversation.ParentPost.Content\" />");
        source.Should().Contain("<PostMediaGrid Media=\"@_conversation.ParentPost.Media\" />");
    }

    [Fact]
    public void PostDetail_ParentQuote_NavigatesToParentPost()
    {
        var source = ReadRepositoryFile("src", "SocialDDD.Client", "Pages", "PostDetail.razor");

        source.Should().Contain("@inject NavigationManager Nav");
        source.Should().Contain("@onclick=\"OpenParentPost\"");
        source.Should().Contain("Nav.NavigateTo($\"/posts/{_conversation.ParentPost.PostId}\")");
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
