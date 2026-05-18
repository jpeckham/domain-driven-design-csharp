using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public sealed class RegisterPendingUserCommandTests
{
    [Fact]
    public async Task Execute_ExistingPendingEmail_ResendsVerificationCodeWithoutRegistrationFields()
    {
        var user = MakePendingUser();
        var userRepo = new FakeUserRepository(user);
        var codeRepo = new FakeVerificationCodeRepository();
        var emailService = new FakeEmailService();
        var command = new RegisterPendingUserCommand(
            userRepo,
            codeRepo,
            new FakePasswordHasher(),
            emailService,
            new FakeDispatcher());

        await command.ExecuteAsync(new RegisterPendingRequest("", "alice@example.com", "", "", ""));

        userRepo.AddedUser.Should().BeNull();
        codeRepo.SavedUserId.Should().Be(user.Id);
        codeRepo.SavedCode.Should().NotBeNull();
        codeRepo.SavedCode!.Code.Should().HaveLength(6);
        emailService.VerificationEmail.Should().Be("alice@example.com");
        emailService.VerificationCode.Should().Be(codeRepo.SavedCode.Code);
    }

    [Fact]
    public async Task Execute_ExistingActiveEmail_DoesNotResendVerificationCode()
    {
        var user = MakePendingUser();
        user.Activate();
        user.PopDomainEvents();

        var userRepo = new FakeUserRepository(user);
        var codeRepo = new FakeVerificationCodeRepository();
        var emailService = new FakeEmailService();
        var command = new RegisterPendingUserCommand(
            userRepo,
            codeRepo,
            new FakePasswordHasher(),
            emailService,
            new FakeDispatcher());

        var act = () => command.ExecuteAsync(new RegisterPendingRequest("", "alice@example.com", "", "", ""));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already registered*");
        codeRepo.SavedCode.Should().BeNull();
        emailService.VerificationEmail.Should().BeNull();
    }

    private static User MakePendingUser()
    {
        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public User? AddedUser { get; private set; }

        public Task AddAsync(User u, CancellationToken ct = default)
        {
            AddedUser = u;
            user = u;
            return Task.CompletedTask;
        }

        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
            Task.FromResult(user?.Id == id ? user : null);

        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
            Task.FromResult(user?.Email == email ? user : null);

        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) =>
            Task.FromResult(user?.Username == username ? user : null);

        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) =>
            Task.FromResult(user?.Handle == handle ? user : null);

        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) =>
            Task.FromResult(user?.Email == email);

        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) =>
            Task.FromResult(user?.Username == username);

        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) =>
            Task.FromResult(user?.Id == id);

        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) =>
            Task.FromResult(user?.Handle == handle);

        public Task UpdateAsync(User u, CancellationToken ct = default)
        {
            user = u;
            return Task.CompletedTask;
        }

        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) =>
            Task.FromResult<User?>(null);
    }

    private sealed class FakeVerificationCodeRepository : IVerificationCodeRepository
    {
        public UserId? SavedUserId { get; private set; }
        public VerificationCode? SavedCode { get; private set; }

        public Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default)
        {
            SavedUserId = userId;
            SavedCode = code;
            return Task.CompletedTask;
        }

        public Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default) =>
            Task.FromResult<VerificationCode?>(null);

        public Task DeleteAsync(UserId userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed-{password}";
        public bool Verify(string password, string passwordHash) => passwordHash == $"hashed-{password}";
    }

    private sealed class FakeEmailService : IEmailService
    {
        public string? VerificationEmail { get; private set; }
        public string? VerificationCode { get; private set; }

        public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
        {
            VerificationEmail = toEmail;
            VerificationCode = code;
            return Task.CompletedTask;
        }

        public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
