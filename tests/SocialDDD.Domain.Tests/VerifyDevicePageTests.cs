using FluentAssertions;

namespace SocialDDD.Domain.Tests;

public class VerifyDevicePageTests
{
    [Fact]
    public void VerifyDevicePage_DoesNotRenderEmailIdentityInformation()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SocialDDD.Client",
            "Pages",
            "VerifyDevice.razor"));

        var markup = File.ReadAllText(pagePath);

        markup.Should().NotContain("type=\"email\"");
        markup.Should().NotContain("value=\"@Email\"");
        markup.Should().NotContain("@bind=\"Email\"");
        markup.Should().NotContain("@Email");
    }
}
