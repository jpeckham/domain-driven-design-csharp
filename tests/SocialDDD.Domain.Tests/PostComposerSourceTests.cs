using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class PostComposerSourceTests
{
    [Fact]
    public void FeedComposer_DisablesPostUntilTextOrUploadedMediaExists()
    {
        var source = ReadClientFile("Pages", "Feed.razor");

        source.Should().Contain("disabled=\"@IsPostDisabled\"");
        source.Should().Contain("private bool IsPostDisabled =>");
        source.Should().Contain("_newMediaAssetIds.Count == 0");
    }

    [Fact]
    public void FeedComposer_RecreatesMediaAttachmentStripAfterSuccessfulPost()
    {
        var source = ReadClientFile("Pages", "Feed.razor");

        source.Should().Contain("@key=\"_newMediaResetKey\"");
        source.Should().Contain("_newMediaResetKey++");
    }

    [Fact]
    public void MediaAttachmentStrip_RemovesFailedUploadsInsteadOfRenderingFailedPreview()
    {
        var source = ReadClientFile("Components", "MediaAttachmentStrip.razor");

        source.Should().Contain("FailUploadAsync");
        source.Should().NotContain("Status = \"Failed\"");
    }

    private static string ReadClientFile(params string[] pathParts)
    {
        var parts = new[]
            {
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "SocialDDD.Client"
            }
            .Concat(pathParts)
            .ToArray();

        return File.ReadAllText(Path.GetFullPath(Path.Combine(parts)));
    }
}
