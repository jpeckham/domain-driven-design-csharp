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
            new PasswordHash("hash123"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

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
            new PasswordHash("h"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Register_InvalidEmail_ThrowsDomainException()
    {
        var act = () => User.Register(
            new Username("alice"),
            new Email("not-an-email"),
            new PasswordHash("h"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PopDomainEvents_ClearsEvents()
    {
        var user = User.Register(
            new Username("bob"),
            new Email("bob@example.com"),
            new PasswordHash("h"),
            new Handle("bob"),
            new DisplayName("Bob"));

        user.PopDomainEvents();
        user.PopDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Register_WithHandleAndDisplayName_SetsPropertiesAndRaisesEvent()
    {
        var handle = new Handle("alice");
        var displayName = new DisplayName("Alice Smith");

        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"),
            handle,
            displayName);

        user.Handle.Should().Be(handle);
        user.DisplayName.Should().Be(displayName);

        var events = user.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegistered>()
            .Which.Handle.Should().Be(handle);
    }

    [Fact]
    public void UpdateDisplayName_ChangesDisplayName()
    {
        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"),
            new Handle("alice"),
            new DisplayName("Alice"));

        user.PopDomainEvents();

        var newName = new DisplayName("Alice Smith");
        user.UpdateDisplayName(newName);

        user.DisplayName.Should().Be(newName);
    }

    [Fact]
    public void Register_CreatesUserWithPendingStatus()
    {
        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

        user.Status.Should().Be(UserStatus.Pending);
    }

    [Fact]
    public void RegisterImmediate_CreatesUserWithActiveStatus()
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Activate_SetsStatusToActive_AndRaisesUserActivatedEvent()
    {
        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

        user.PopDomainEvents();

        user.Activate();

        user.Status.Should().Be(UserStatus.Active);

        var events = user.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<UserActivated>()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsDomainException()
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash123"),
            new Handle("alice"),
            new DisplayName("Alice Smith"));

        var act = () => user.Activate();
        act.Should().Throw<DomainException>();
    }
}
