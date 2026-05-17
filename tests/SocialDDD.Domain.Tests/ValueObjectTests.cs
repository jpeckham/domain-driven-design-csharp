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

    // Handle tests
    [Theory]
    [InlineData("alice")]
    [InlineData("alice_123")]
    [InlineData("a")]
    public void Handle_ValidInput_CreatesHandle(string input)
    {
        var handle = new Handle(input);
        handle.Value.Should().Be(input.ToLowerInvariant());
        handle.Display.Should().Be("@" + input.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("alice smith")]
    [InlineData("@alice")]
    [InlineData("alice!")]
    [InlineData("alice-bob")]
    [InlineData(null!)]
    public void Handle_InvalidInput_ThrowsDomainException(string input)
    {
        var act = () => new Handle(input);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Handle_Equality_IsCaseInsensitive()
    {
        var h1 = new Handle("Alice");
        var h2 = new Handle("alice");
        h1.Should().Be(h2);
    }

    [Fact]
    public void Handle_MaxLength_IsAccepted()
    {
        var thirtyChars = new string('a', 30);
        var act = () => new Handle(thirtyChars);
        act.Should().NotThrow();
    }

    [Fact]
    public void Handle_TooLong_ThrowsDomainException()
    {
        var thirtyOneChars = new string('a', 31);
        var act = () => new Handle(thirtyOneChars);
        act.Should().Throw<DomainException>();
    }

    // DisplayName tests
    [Theory]
    [InlineData("Alice Smith")]
    [InlineData("  Alice  ")]
    [InlineData("A")]
    public void DisplayName_ValidInput_CreatesDisplayName(string input)
    {
        var dn = new DisplayName(input);
        dn.Value.Should().Be(input.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void DisplayName_Empty_ThrowsDomainException(string input)
    {
        var act = () => new DisplayName(input);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DisplayName_TooLong_ThrowsDomainException()
    {
        var fiftyOneChars = new string('a', 51);
        var act = () => new DisplayName(fiftyOneChars);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DisplayName_MaxLength_IsAccepted()
    {
        var fiftyChars = new string('a', 50);
        var act = () => new DisplayName(fiftyChars);
        act.Should().NotThrow();
    }

    [Fact]
    public void DisplayName_TrimsWhitespace()
    {
        var dn = new DisplayName("  Alice  ");
        dn.Value.Should().Be("Alice");
    }
}
