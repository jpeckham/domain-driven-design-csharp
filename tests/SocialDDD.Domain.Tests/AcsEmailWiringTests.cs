using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public sealed class AcsEmailWiringTests
{
    [Fact]
    public void AzureCommunicationEmailService_IsImplementedWithAcsSdk()
    {
        var service = ReadRepositoryFile("src", "SocialDDD.Infrastructure", "Identity", "Emails", "AzureCommunicationEmailService.cs");
        var project = ReadRepositoryFile("src", "SocialDDD.Infrastructure", "SocialDDD.Infrastructure.csproj");

        service.Should().Contain("EmailClient");
        service.Should().Contain("AcsEmail");
        service.Should().NotContain("NotImplementedException");
        project.Should().Contain("Azure.Communication.Email");
    }

    [Fact]
    public void ProductionInfrastructure_UsesSharedCleanSocialAcsEmailSecrets()
    {
        var source = ReadRepositoryFile("infrastructure", "bicep", "main.bicep");

        source.Should().Contain("Features__EmailService");
        source.Should().Contain("'AzureCommunication'");
        source.Should().Contain("cleansocial-${environmentName}-acs-email-connection-string");
        source.Should().Contain("cleansocial-${environmentName}-acs-email-sender-address");
        source.Should().Contain("AcsEmail__ConnectionString");
        source.Should().Contain("AcsEmail__SenderAddress");
    }

    private static string ReadRepositoryFile(params string[] relativePathParts)
    {
        var relativePath = Path.Combine(relativePathParts);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relativePath)))
            dir = dir.Parent;

        if (dir is null)
            throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}");

        return File.ReadAllText(Path.Combine(dir.FullName, relativePath));
    }
}
