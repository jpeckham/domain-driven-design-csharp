using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

// ---- Value object tests ----

public class DeviceIdTests
{
    [Fact]
    public void DeviceId_ValidGuid_CreatesDeviceId()
    {
        var guid = Guid.NewGuid().ToString();
        var deviceId = new DeviceId(guid);
        deviceId.Value.Should().Be(guid.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void DeviceId_NullOrEmpty_ThrowsDomainValidationException(string input)
    {
        var act = () => new DeviceId(input);
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
    public void DeviceId_InvalidGuid_ThrowsDomainValidationException(string input)
    {
        var act = () => new DeviceId(input);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void DeviceId_New_CreatesValidDeviceId()
    {
        var deviceId = DeviceId.New();
        deviceId.Value.Should().NotBeNullOrEmpty();
        Guid.TryParse(deviceId.Value, out _).Should().BeTrue();
    }
}

public class OneTimePasscodeTests
{
    [Fact]
    public void OneTimePasscode_Valid_CreatesPasscode()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("123456", now.AddMinutes(10));
        otp.Code.Should().Be("123456");
        otp.IsExpired(now).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void OneTimePasscode_Empty_ThrowsDomainValidationException(string code)
    {
        var act = () => new OneTimePasscode(code, DateTimeOffset.UtcNow.AddMinutes(10));
        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("12345")]     // too short
    [InlineData("1234567")]   // too long
    [InlineData("12345a")]    // non-digit
    [InlineData("ABCDEF")]    // letters
    public void OneTimePasscode_InvalidFormat_ThrowsDomainValidationException(string code)
    {
        var act = () => new OneTimePasscode(code, DateTimeOffset.UtcNow.AddMinutes(10));
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void OneTimePasscode_NotExpired_IsExpiredReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("000000", now.AddMinutes(10));
        otp.IsExpired(now).Should().BeFalse();
    }

    [Fact]
    public void OneTimePasscode_Expired_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("000000", now.AddMinutes(-1));
        otp.IsExpired(now).Should().BeTrue();
    }

    [Fact]
    public void OneTimePasscode_ExpiresExactlyNow_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("000000", now);
        otp.IsExpired(now).Should().BeTrue();
    }
}

// ---- Handler tests ----

public class LoginWithDeviceHandlerTests
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
    public async Task Execute_KnownDevice_ReturnsSuccess()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();

        var userRepo = new FakeUserRepo(user);
        var hasher = new FakePasswordHasher(valid: true);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: true);
        var otpRepo = new FakeOtpRepo();
        var emailSvc = new FakeEmailService();
        var tokenSvc = new FakeTokenService();

        var handler = new LoginWithDeviceCommand(userRepo, hasher, deviceRepo, otpRepo, emailSvc, tokenSvc);
        var result = await handler.ExecuteAsync(new LoginWithDeviceRequest("alice@example.com", "password", deviceId.Value));

        result.Should().BeOfType<LoginWithDeviceResult.Success>();
        var success = (LoginWithDeviceResult.Success)result;
        success.Token.Should().Be("fake-token");
        emailSvc.OtpSentCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_UnknownDevice_SendsOtpAndReturnsOtpRequired()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();

        var userRepo = new FakeUserRepo(user);
        var hasher = new FakePasswordHasher(valid: true);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var otpRepo = new FakeOtpRepo();
        var emailSvc = new FakeEmailService();
        var tokenSvc = new FakeTokenService();

        var handler = new LoginWithDeviceCommand(userRepo, hasher, deviceRepo, otpRepo, emailSvc, tokenSvc);
        var result = await handler.ExecuteAsync(new LoginWithDeviceRequest("alice@example.com", "password", deviceId.Value));

        result.Should().BeOfType<LoginWithDeviceResult.OtpRequired>();
        emailSvc.OtpSentCount.Should().Be(1);
        otpRepo.SavedOtp.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_WrongPassword_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();

        var userRepo = new FakeUserRepo(user);
        var hasher = new FakePasswordHasher(valid: false);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var otpRepo = new FakeOtpRepo();
        var emailSvc = new FakeEmailService();
        var tokenSvc = new FakeTokenService();

        var handler = new LoginWithDeviceCommand(userRepo, hasher, deviceRepo, otpRepo, emailSvc, tokenSvc);
        var act = () => handler.ExecuteAsync(new LoginWithDeviceRequest("alice@example.com", "wrong", deviceId.Value));

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Execute_UserNotFound_ThrowsDomainValidationException()
    {
        var deviceId = DeviceId.New();
        var userRepo = new FakeUserRepo(null);
        var hasher = new FakePasswordHasher(valid: true);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var otpRepo = new FakeOtpRepo();
        var emailSvc = new FakeEmailService();
        var tokenSvc = new FakeTokenService();

        var handler = new LoginWithDeviceCommand(userRepo, hasher, deviceRepo, otpRepo, emailSvc, tokenSvc);
        var act = () => handler.ExecuteAsync(new LoginWithDeviceRequest("nobody@example.com", "password", deviceId.Value));

        await act.Should().ThrowAsync<DomainValidationException>();
    }
}

public class VerifyDeviceOtpHandlerTests
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
    public async Task Execute_CorrectOtp_ReturnsToken()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, deviceId, otp);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var tokenSvc = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenSvc);
        var result = await handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", deviceId.Value, "123456", false));

        result.Token.Should().Be("fake-token");
        otpRepo.Deleted.Should().BeTrue();
        deviceRepo.RememberedCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_CorrectOtp_RememberDevice_RemembersDevice()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, deviceId, otp);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var tokenSvc = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenSvc);
        await handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", deviceId.Value, "123456", true));

        deviceRepo.RememberedCount.Should().Be(1);
    }

    [Fact]
    public async Task Execute_WrongOtp_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, deviceId, otp);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var tokenSvc = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenSvc);
        var act = () => handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", deviceId.Value, "000000", false));

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*Invalid OTP*");
    }

    [Fact]
    public async Task Execute_ExpiredOtp_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(-1));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, deviceId, otp);
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var tokenSvc = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenSvc);
        var act = () => handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", deviceId.Value, "123456", false));

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task Execute_NoOtpFound_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var deviceId = DeviceId.New();

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(); // no OTP stored
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var tokenSvc = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenSvc);
        var act = () => handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", deviceId.Value, "123456", false));

        await act.Should().ThrowAsync<DomainValidationException>();
    }
}

// ---- Fakes shared by handler tests ----

file sealed class FakeUserRepo(User? user) : IUserRepository
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

file sealed class FakePasswordHasher(bool valid) : IPasswordHasher
{
    public string Hash(string password) => "hashed";
    public bool Verify(string password, string hash) => valid;
}

file sealed class FakeRememberedDeviceRepo(bool isRemembered) : IRememberedDeviceRepository
{
    public int RememberedCount { get; private set; }

    public Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
        => Task.FromResult(isRemembered);

    public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        RememberedCount++;
        return Task.CompletedTask;
    }
}

file sealed class FakeOtpRepo : IOtpRepository
{
    private readonly UserId? _userId;
    private readonly DeviceId? _deviceId;
    private readonly OneTimePasscode? _otp;

    public OneTimePasscode? SavedOtp { get; private set; }
    public bool Deleted { get; private set; }

    public FakeOtpRepo() { }

    public FakeOtpRepo(UserId userId, DeviceId deviceId, OneTimePasscode otp)
    {
        _userId = userId;
        _deviceId = deviceId;
        _otp = otp;
    }

    public Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default)
    {
        SavedOtp = otp;
        return Task.CompletedTask;
    }

    public Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        if (_userId == userId && _deviceId == deviceId)
            return Task.FromResult(_otp);
        return Task.FromResult<OneTimePasscode?>(null);
    }

    public Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        Deleted = true;
        return Task.CompletedTask;
    }
}

file sealed class FakeEmailService : IEmailService
{
    public int OtpSentCount { get; private set; }

    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        OtpSentCount++;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string token, CancellationToken ct = default)
        => Task.CompletedTask;
}

file sealed class FakeTokenService : ITokenService
{
    public string GenerateToken(User user) => "fake-token";
}
