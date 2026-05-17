using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Register_ValidArgs_CreatesUserAndRaisesEvent()
    {
        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"));

        user.Username.Value.Should().Be("alice");
        user.Email.Value.Should().Be("alice@example.com");
        user.Id.Should().NotBeNull();

        var events = user.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegistered>()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Register_EmptyUsername_ThrowsDomainException()
    {
        var act = () => User.Register(
            new Username(""),
            new Email("a@b.com"),
            new PasswordHash("h"));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Register_InvalidEmail_ThrowsDomainException()
    {
        var act = () => User.Register(
            new Username("alice"),
            new Email("not-an-email"),
            new PasswordHash("h"));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PopDomainEvents_ClearsEvents()
    {
        var user = User.Register(
            new Username("bob"),
            new Email("bob@example.com"),
            new PasswordHash("h"));

        user.PopDomainEvents();
        user.PopDomainEvents().Should().BeEmpty();
    }
}
