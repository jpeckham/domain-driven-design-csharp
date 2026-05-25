using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public sealed class MongoCosmosIndexCompatibilityTests
{
    [Fact]
    public void MongoDbContext_DoesNotCreateUniqueSecondaryIndexes()
    {
        var source = File.ReadAllText(FindRepoFile(Path.Combine(
            "src",
            "SocialDDD.Infrastructure",
            "Persistence",
            "MongoDbContext.cs")));

        source.Should().NotContain("Unique = true");
    }

    [Fact]
    public void PostRepository_DoesNotUseMongoTextSearch()
    {
        var source = File.ReadAllText(FindRepoFile(Path.Combine(
            "src",
            "SocialDDD.Infrastructure",
            "Social",
            "Persistence",
            "Posts",
            "PostRepository.cs")));

        source.Should().NotContain("Filter.Text");
        source.Should().NotContain("MetaTextScore");
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relativePath)))
            dir = dir.Parent;

        if (dir is null)
            throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}");

        return Path.Combine(dir.FullName, relativePath);
    }
}
