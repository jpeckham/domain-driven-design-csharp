using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class VerifyRegistrationHandlerTests
{
    private static User MakePendingUser(string emailValue = "alice@example.com")
    {
        var user = User.Register(
            new Username("alice"),
            new Email(emailValue),
            new PasswordHash("hash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    [Fact]
    public async Task Execute_CorrectCode_ActivatesUserAndReturnsToken()
    {
        var user = MakePendingUser();
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode("123456", now.AddMinutes(15));

        var userRepo = new FakeUserRepository(user);
        var codeRepo = new FakeCodeRepository(user.Id, code);
        var tokenService = new FakeTokenService();
        var dispatcher = new FakeDispatcher();

        var handler = new VerifyRegistrationCommand(userRepo, codeRepo, tokenService, dispatcher);
        var result = await handler.ExecuteAsync(new VerifyRegistrationRequest("alice@example.com", "123456"));

        result.Token.Should().Be("fake-token");
        user.Status.Should().Be(UserStatus.Active);
        codeRepo.Deleted.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_WrongCode_ThrowsDomainException()
    {
        var user = MakePendingUser();
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode("123456", now.AddMinutes(15));

        var userRepo = new FakeUserRepository(user);
        var codeRepo = new FakeCodeRepository(user.Id, code);
        var tokenService = new FakeTokenService();
        var dispatcher = new FakeDispatcher();

        var handler = new VerifyRegistrationCommand(userRepo, codeRepo, tokenService, dispatcher);
        var act = () => handler.ExecuteAsync(new VerifyRegistrationRequest("alice@example.com", "000000"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Invalid*");
    }

    [Fact]
    public async Task Execute_ExpiredCode_ThrowsDomainException()
    {
        var user = MakePendingUser();
        var now = DateTimeOffset.UtcNow;
        var code = new VerificationCode("123456", now.AddMinutes(-1));

        var userRepo = new FakeUserRepository(user);
        var codeRepo = new FakeCodeRepository(user.Id, code);
        var tokenService = new FakeTokenService();
        var dispatcher = new FakeDispatcher();

        var handler = new VerifyRegistrationCommand(userRepo, codeRepo, tokenService, dispatcher);
        var act = () => handler.ExecuteAsync(new VerifyRegistrationRequest("alice@example.com", "123456"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task Execute_UserNotFound_ThrowsDomainException()
    {
        var userRepo = new FakeUserRepository(null);
        var codeRepo = new FakeCodeRepository(null, null);
        var tokenService = new FakeTokenService();
        var dispatcher = new FakeDispatcher();

        var handler = new VerifyRegistrationCommand(userRepo, codeRepo, tokenService, dispatcher);
        var act = () => handler.ExecuteAsync(new VerifyRegistrationRequest("nobody@example.com", "123456"));

        await act.Should().ThrowAsync<DomainException>();
    }

    // ---- fakes ----

    private sealed class FakeUserRepository(User? user) : IUserRepository
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
    }

    private sealed class FakeCodeRepository(UserId? userId, VerificationCode? code) : IVerificationCodeRepository
    {
        public bool Deleted { get; private set; }

        public Task SaveAsync(UserId id, VerificationCode c, CancellationToken ct = default) => Task.CompletedTask;
        public Task<VerificationCode?> FindByUserIdAsync(UserId id, CancellationToken ct = default)
            => Task.FromResult(userId == id ? code : null);
        public Task DeleteAsync(UserId id, CancellationToken ct = default) { Deleted = true; return Task.CompletedTask; }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateToken(User user) => "fake-token";
    }

    private sealed class FakeDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
