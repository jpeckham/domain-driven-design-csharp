using System.Collections.Concurrent;
using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Domain.Tests;

// ---- Value object tests ----

public class PasswordResetTokenTests
{
    [Fact]
    public void PasswordResetToken_Valid_CreatesToken()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken("abc123", now.AddMinutes(5));

        token.Token.Should().Be("abc123");
        token.IsExpired(now).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void PasswordResetToken_NullOrEmpty_ThrowsDomainValidationException(string token)
    {
        var act = () => new PasswordResetToken(token, DateTimeOffset.UtcNow.AddMinutes(5));
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void PasswordResetToken_NotExpired_IsExpiredReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken("tok", now.AddMinutes(5));
        token.IsExpired(now).Should().BeFalse();
    }

    [Fact]
    public void PasswordResetToken_Expired_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken("tok", now.AddMinutes(-1));
        token.IsExpired(now).Should().BeTrue();
    }

    [Fact]
    public void PasswordResetToken_ExpiresExactlyNow_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken("tok", now);
        token.IsExpired(now).Should().BeTrue();
    }
}

// ---- User domain tests ----

public class UserResetPasswordTests
{
    private static User MakeActiveUser()
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("oldhash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    [Fact]
    public void ResetPassword_UpdatesHashAndRaisesPasswordResetEvent()
    {
        var user = MakeActiveUser();
        var newHash = new PasswordHash("newhash");

        user.ResetPassword(newHash);

        user.PasswordHash.Value.Should().Be("newhash");
        var events = user.PopDomainEvents();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<PasswordReset>()
            .Which.UserId.Should().Be(user.Id);
    }
}

// ---- Handler tests ----

public class RequestPasswordResetHandlerTests
{
    private static User MakeActiveUser(string emailValue = "alice@example.com")
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email(emailValue),
            new PasswordHash("hashed"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    [Fact]
    public async Task Execute_UserNotFound_SilentlySucceeds()
    {
        var userRepo = new FakeUserRepoForReset(null);
        var tokenRepo = new FakePasswordResetTokenRepo();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new RequestPasswordResetCommand(userRepo, tokenRepo, dispatcher);
        // Should not throw
        await handler.ExecuteAsync("nobody@example.com");

        dispatcher.DispatchedEvents.Should().BeEmpty();
        tokenRepo.SavedCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_ValidEmail_SavesTokenAndSendsEmail()
    {
        var user = MakeActiveUser();
        var userRepo = new FakeUserRepoForReset(user);
        var tokenRepo = new FakePasswordResetTokenRepo();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new RequestPasswordResetCommand(userRepo, tokenRepo, dispatcher);
        await handler.ExecuteAsync("alice@example.com");

        tokenRepo.SavedCount.Should().Be(1);
        tokenRepo.LastToken.Should().NotBeNullOrEmpty();
        dispatcher.DispatchedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PasswordResetRequested>()
            .Which.Token.Should().Be(tokenRepo.LastToken);
    }

    [Fact]
    public async Task Execute_InvalidEmail_SilentlySucceeds()
    {
        var userRepo = new FakeUserRepoForReset(null);
        var tokenRepo = new FakePasswordResetTokenRepo();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new RequestPasswordResetCommand(userRepo, tokenRepo, dispatcher);
        // Invalid email — should silently succeed without throwing
        await handler.ExecuteAsync("not-an-email");

        dispatcher.DispatchedEvents.Should().BeEmpty();
    }
}

public class ResetPasswordHandlerTests
{
    private static User MakeActiveUser(string emailValue = "alice@example.com")
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email(emailValue),
            new PasswordHash("oldhash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    [Fact]
    public async Task Execute_ValidToken_UpdatesPasswordAndDeletesToken()
    {
        var user = MakeActiveUser();
        var token = new PasswordResetToken("valid-token", DateTimeOffset.UtcNow.AddMinutes(5));
        var tokenRepo = new FakePasswordResetTokenRepo(user.Id, token);
        var userRepo = new FakeUserRepoForReset(user);
        var hasher = new FakePasswordHasherForReset();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new ResetPasswordCommand(userRepo, tokenRepo, hasher, dispatcher);
        await handler.ExecuteAsync("valid-token", "NewPassword123");

        user.PasswordHash.Value.Should().Be("hashed");
        tokenRepo.Deleted.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ExpiredToken_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var token = new PasswordResetToken("valid-token", DateTimeOffset.UtcNow.AddMinutes(-1));
        var tokenRepo = new FakePasswordResetTokenRepo(user.Id, token);
        var userRepo = new FakeUserRepoForReset(user);
        var hasher = new FakePasswordHasherForReset();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new ResetPasswordCommand(userRepo, tokenRepo, hasher, dispatcher);
        var act = () => handler.ExecuteAsync("valid-token", "NewPassword123");

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task Execute_InvalidToken_ThrowsDomainValidationException()
    {
        var tokenRepo = new FakePasswordResetTokenRepo(); // no tokens stored
        var userRepo = new FakeUserRepoForReset(null);
        var hasher = new FakePasswordHasherForReset();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new ResetPasswordCommand(userRepo, tokenRepo, hasher, dispatcher);
        var act = () => handler.ExecuteAsync("wrong-token", "NewPassword123");

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*Invalid*");
    }

    [Fact]
    public async Task Execute_WeakPassword_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var token = new PasswordResetToken("valid-token", DateTimeOffset.UtcNow.AddMinutes(5));
        var tokenRepo = new FakePasswordResetTokenRepo(user.Id, token);
        var userRepo = new FakeUserRepoForReset(user);
        var hasher = new FakePasswordHasherForReset();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new ResetPasswordCommand(userRepo, tokenRepo, hasher, dispatcher);
        var act = () => handler.ExecuteAsync("valid-token", "short"); // less than 8 chars

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Execute_TokenUsedTwice_SecondCallThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var token = new PasswordResetToken("one-time-token", DateTimeOffset.UtcNow.AddMinutes(5));
        var tokenRepo = new FakePasswordResetTokenRepo(user.Id, token);
        var userRepo = new FakeUserRepoForReset(user);
        var hasher = new FakePasswordHasherForReset();
        var dispatcher = new FakeDispatcherForReset();

        var handler = new ResetPasswordCommand(userRepo, tokenRepo, hasher, dispatcher);

        // First call succeeds
        await handler.ExecuteAsync("one-time-token", "NewPassword123");

        // Second call: token was deleted, so it should fail
        var act = () => handler.ExecuteAsync("one-time-token", "NewPassword456");
        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*Invalid*");
    }
}

// ---- Fakes ----

file sealed class FakeUserRepoForReset(User? user) : IUserRepository
{
    private User? _user = user;

    public Task AddAsync(User u, CancellationToken ct = default) { _user = u; return Task.CompletedTask; }
    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(_user?.Id == id ? _user : null);
    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(_user?.Email == email ? _user : null);
    public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(_user?.Username == username ? _user : null);
    public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(_user?.Handle == handle ? _user : null);
    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(_user?.Email == email);
    public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(_user?.Username == username);
    public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(_user?.Id == id);
    public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(_user?.Handle == handle);
    public Task UpdateAsync(User u, CancellationToken ct = default) { _user = u; return Task.CompletedTask; }
    public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
}

file sealed class FakePasswordResetTokenRepo : IPasswordResetTokenRepository
{
    private readonly ConcurrentDictionary<string, (UserId UserId, PasswordResetToken Token)> _byToken = new();
    private readonly ConcurrentDictionary<string, string> _userIdToToken = new();

    public int SavedCount { get; private set; }
    public string? LastToken { get; private set; }
    public bool Deleted { get; private set; }

    public FakePasswordResetTokenRepo() { }

    public FakePasswordResetTokenRepo(UserId userId, PasswordResetToken token)
    {
        _byToken[token.Token] = (userId, token);
        _userIdToToken[userId.Value.ToString()] = token.Token;
        LastToken = token.Token;
        SavedCount = 1;
    }

    public Task SaveAsync(UserId userId, PasswordResetToken token, CancellationToken ct = default)
    {
        SavedCount++;
        LastToken = token.Token;
        _byToken[token.Token] = (userId, token);
        _userIdToToken[userId.Value.ToString()] = token.Token;
        return Task.CompletedTask;
    }

    public Task<(UserId UserId, PasswordResetToken Token)?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        if (_byToken.TryGetValue(token, out var entry))
            return Task.FromResult<(UserId, PasswordResetToken)?>(entry);
        return Task.FromResult<(UserId, PasswordResetToken)?>(null);
    }

    public Task DeleteByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        Deleted = true;
        var key = userId.Value.ToString();
        if (_userIdToToken.TryRemove(key, out var tok))
            _byToken.TryRemove(tok, out _);
        return Task.CompletedTask;
    }
}

file sealed class FakePasswordHasherForReset : IPasswordHasher
{
    public string Hash(string password) => "hashed";
    public bool Verify(string password, string hash) => true;
}

file sealed class FakeDispatcherForReset : IDomainEventDispatcher
{
    public IReadOnlyList<IDomainEvent> DispatchedEvents { get; private set; } = [];

    public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        DispatchedEvents = events;
        return Task.CompletedTask;
    }
}
