using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class ValueObjectTests
{
    [Theory]
    [InlineData("ALICE@EXAMPLE.COM", "alice@example.com")]
    [InlineData("  Bob@Test.Org  ", "bob@test.org")]
    public void Email_NormalisesToLowercase(string input, string expected)
    {
        new Email(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData(null!)]
    public void Email_InvalidInput_ThrowsDomainException(string input)
    {
        var act = () => new Email(input);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Username_OverLimit_ThrowsDomainException()
    {
        var act = () => new Username(new string('a', 51));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PostContent_Exactly280Chars_Succeeds()
    {
        var act = () => new PostContent(new string('x', 280));
        act.Should().NotThrow();
    }

    [Fact]
    public void PostContent_281Chars_ThrowsDomainException()
    {
        var act = () => new PostContent(new string('x', 281));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UserId_ValueEquality()
    {
        var id = Guid.NewGuid();
        UserId.From(id).Should().Be(UserId.From(id));
    }
}
