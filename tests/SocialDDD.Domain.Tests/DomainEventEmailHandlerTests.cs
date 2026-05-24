using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Identity.Users;
using SocialDDD.Domain.Identity.Users.Events;
using SocialDDD.Infrastructure;

namespace SocialDDD.Domain.Tests;

public sealed class DomainEventEmailHandlerTests
{
    [Fact]
    public async Task UserRegisteredHandler_SavesVerificationCodeAndSendsEmail()
    {
        var user = MakeUser();
        var codeRepo = new FakeVerificationCodeRepository();
        var emailService = new FakeEmailService();
        await using var provider = BuildProvider(user, codeRepo, emailService);

        var handler = provider.GetRequiredService<IDomainEventHandler<UserRegistered>>();

        await handler.HandleAsync(new UserRegistered(user.Id, user.Email, user.Handle, user.DisplayName));

        codeRepo.SavedUserId.Should().Be(user.Id);
        codeRepo.SavedCode.Should().NotBeNull();
        codeRepo.SavedCode!.Code.Should().HaveLength(6);
        emailService.VerificationEmail.Should().Be(user.Email.Value);
        emailService.VerificationCode.Should().Be(codeRepo.SavedCode.Code);
    }

    [Fact]
    public async Task PasswordResetRequestedHandler_SendsResetEmail()
    {
        var user = MakeUser();
        var emailService = new FakeEmailService();
        await using var provider = BuildProvider(user, new FakeVerificationCodeRepository(), emailService);
        var handler = provider.GetRequiredService<IDomainEventHandler<PasswordResetRequested>>();

        await handler.HandleAsync(new PasswordResetRequested(user.Id, user.Email, "reset-token"));

        emailService.PasswordResetEmail.Should().Be(user.Email.Value);
        emailService.PasswordResetToken.Should().Be("reset-token");
    }

    [Fact]
    public async Task UserVerificationRequestedHandler_SendsVerificationEmail()
    {
        var user = MakeUser();
        var emailService = new FakeEmailService();
        await using var provider = BuildProvider(user, new FakeVerificationCodeRepository(), emailService);
        var handler = provider.GetRequiredService<IDomainEventHandler<UserVerificationRequested>>();

        await handler.HandleAsync(new UserVerificationRequested(user.Id, user.Email, "654321"));

        emailService.VerificationEmail.Should().Be(user.Email.Value);
        emailService.VerificationCode.Should().Be("654321");
    }

    [Fact]
    public async Task LoginChallengedHandler_SendsOtpEmail()
    {
        var user = MakeUser();
        var emailService = new FakeEmailService();
        await using var provider = BuildProvider(user, new FakeVerificationCodeRepository(), emailService);
        var handler = provider.GetRequiredService<IDomainEventHandler<LoginChallenged>>();

        await handler.HandleAsync(new LoginChallenged(user.Id, user.Email, DeviceId.New(), "123456"));

        emailService.OtpEmail.Should().Be(user.Email.Value);
        emailService.OtpCode.Should().Be("123456");
    }

    private static ServiceProvider BuildProvider(
        User user,
        FakeVerificationCodeRepository codeRepository,
        FakeEmailService emailService)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        services.AddSingleton<IUserRepository>(new FakeUserRepository(user));
        services.AddSingleton<IVerificationCodeRepository>(codeRepository);
        services.AddSingleton<IEmailService>(emailService);
        return services.BuildServiceProvider();
    }

    private static User MakeUser()
    {
        var user = User.Register(
            new Username("alice"),
            new Email("alice@example.com"),
            new PasswordHash("hashed"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task AddAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user.Id == id ? user : null);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(user.Email == email ? user : null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(user.Username == username ? user : null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user.Handle == handle ? user : null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(user.Email == email);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(user.Username == username);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user.Id == id);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user.Handle == handle);
        public Task UpdateAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> FindByProfileImageAssetIdAsync(Guid assetId, CancellationToken ct = default) => Task.FromResult<User?>(null);
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

    private sealed class FakeEmailService : IEmailService
    {
        public string? VerificationEmail { get; private set; }
        public string? VerificationCode { get; private set; }
        public string? PasswordResetEmail { get; private set; }
        public string? PasswordResetToken { get; private set; }
        public string? OtpEmail { get; private set; }
        public string? OtpCode { get; private set; }

        public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
        {
            VerificationEmail = toEmail;
            VerificationCode = code;
            return Task.CompletedTask;
        }

        public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
        {
            OtpEmail = toEmail;
            OtpCode = otp;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken ct = default)
        {
            PasswordResetEmail = toEmail;
            PasswordResetToken = token;
            return Task.CompletedTask;
        }
    }
}
