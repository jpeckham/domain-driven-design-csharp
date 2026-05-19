using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class PostCardSourceTests
{
    [Fact]
    public void RepostButton_IsDisabledForReposts()
    {
        var source = ReadClientFile("Components", "PostCard.razor");

        source.Should().Contain("IsRepost");
        source.Should().Contain("disabled=\"@IsRepost\"");
        source.Should().Contain("title=\"@RepostTitle\"");
    }

    [Fact]
    public void RepostButton_OnlyInvokesCallbackForOriginalPosts()
    {
        var source = ReadClientFile("Components", "PostCard.razor");

        source.Should().Contain("RepostAsync");
        source.Should().Contain("if (IsRepost) return;");
        source.Should().Contain("OnRepost.InvokeAsync(Post.PostId)");
    }

    private static string ReadClientFile(params string[] pathParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SocialDDD.sln")))
            dir = dir.Parent;

        var root = dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        return File.ReadAllText(Path.Combine(new[] { root, "src", "SocialDDD.Client" }.Concat(pathParts).ToArray()));
    }
}
