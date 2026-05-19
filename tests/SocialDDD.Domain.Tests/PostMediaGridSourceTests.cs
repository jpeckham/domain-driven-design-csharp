using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class PostMediaGridSourceTests
{
    [Fact]
    public void PostMediaGrid_UsesApiMediaUrlProperty()
    {
        var serviceSource = ReadClientFile("Services", "PostApiService.cs");
        var gridSource = ReadClientFile("Components", "PostMediaGrid.razor");

        serviceSource.Should().Contain("string MediaUrl");
        gridSource.Should().Contain("@item.MediaUrl");
        gridSource.Should().NotContain("@item.Url");
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
