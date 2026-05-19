using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class DockerComposeStorageTests
{
    [Fact]
    public void ApiService_PersistsProfileImagesAlongsidePostMedia()
    {
        var source = File.ReadAllText(FindRepoFile("docker-compose.yml"));

        source.Should().Contain("PostMedia__Directory: /app/data/post-media");
        source.Should().Contain("ProfileImages__Directory: /app/data/post-media/profile-images");
        source.Should().Contain("- post_media_data:/app/data/post-media");
    }

    [Fact]
    public void ApiService_PersistsDeviceOtpAndRememberedDevicesForLocalDocker()
    {
        var source = File.ReadAllText(FindRepoFile("docker-compose.yml"));

        source.Should().Contain("Features__OtpRepository: MongoDb");
        source.Should().Contain("Features__RememberedDeviceRepository: MongoDb");
    }

    private static string FindRepoFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, fileName)))
            dir = dir.Parent;

        if (dir is null)
            throw new FileNotFoundException($"Could not find {fileName} from {AppContext.BaseDirectory}");

        return Path.Combine(dir.FullName, fileName);
    }
}
