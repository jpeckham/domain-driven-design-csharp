# Device-Based MFA Login with OTP — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional device-tracking + OTP email flow so that login from an unrecognized device triggers a 6-digit OTP via email rather than issuing a token directly.

**Architecture:** Two new domain repository interfaces (`IRememberedDeviceRepository`, `IOtpRepository`) hold device trust and pending OTPs. Two new application command handlers (`LoginWithDeviceCommand`, `VerifyDeviceOtpCommand`) orchestrate credential check, device check, OTP generation/validation, and token issuance. A new `SessionsController` exposes `POST /api/sessions/device` and `POST /api/sessions/device/verify`.

**Tech Stack:** .NET 9, xUnit, FluentAssertions, MongoDB.Driver, BCrypt (existing PasswordHasher), System.Security.Cryptography (RandomNumberGenerator, CryptographicOperations).

---

## File Map

**Create:**
- `src/SocialDDD.Domain/Users/DeviceId.cs` — value object wrapping a GUID string
- `src/SocialDDD.Domain/Users/OneTimePasscode.cs` — 6-digit OTP value object with expiry
- `src/SocialDDD.Domain/Users/IRememberedDeviceRepository.cs` — domain repo interface
- `src/SocialDDD.Domain/Users/IOtpRepository.cs` — domain repo interface
- `src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs` — handler + result discriminated union
- `src/SocialDDD.Application/Users/Commands/VerifyDeviceOtpCommand.cs` — handler
- `src/SocialDDD.Application/Users/DTOs/LoginWithDeviceRequest.cs` — API DTO
- `src/SocialDDD.Application/Users/DTOs/VerifyDeviceOtpRequest.cs` — API DTO
- `src/SocialDDD.Infrastructure/Persistence/Devices/InMemoryRememberedDeviceRepository.cs`
- `src/SocialDDD.Infrastructure/Persistence/Devices/MongoDbRememberedDeviceRepository.cs`
- `src/SocialDDD.Infrastructure/Persistence/Otps/InMemoryOtpRepository.cs`
- `src/SocialDDD.Infrastructure/Persistence/Otps/MongoDbOtpRepository.cs`
- `src/SocialDDD.Api/Controllers/SessionsController.cs`
- `tests/SocialDDD.Domain.Tests/DeviceMfaValueObjectTests.cs`
- `tests/SocialDDD.Domain.Tests/LoginWithDeviceHandlerTests.cs`
- `tests/SocialDDD.Domain.Tests/VerifyDeviceOtpHandlerTests.cs`

**Modify:**
- `src/SocialDDD.Application/Interfaces/IEmailService.cs` — add `SendOtpEmailAsync`
- `src/SocialDDD.Infrastructure/Emails/ConsoleEmailService.cs` — implement new method
- `src/SocialDDD.Infrastructure/Emails/AzureCommunicationEmailService.cs` — implement new method
- `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs` — add two new collections + indexes
- `src/SocialDDD.Infrastructure/DependencyInjection.cs` — register new repos and commands

---

## Task 1: `DeviceId` value object

**Files:**
- Create: `src/SocialDDD.Domain/Users/DeviceId.cs`
- Test: `tests/SocialDDD.Domain.Tests/DeviceMfaValueObjectTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/SocialDDD.Domain.Tests/DeviceMfaValueObjectTests.cs
using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class DeviceMfaValueObjectTests
{
    // ---- DeviceId ----

    [Fact]
    public void DeviceId_ValidGuid_Succeeds()
    {
        var guid = Guid.NewGuid().ToString();
        var deviceId = new DeviceId(guid);
        deviceId.Value.Should().Be(guid);
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("12345678-1234-1234-1234-12345678901Z")]
    public void DeviceId_Invalid_ThrowsDomainValidationException(string input)
    {
        var act = () => new DeviceId(input);
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void DeviceId_Equality_WorksByValue()
    {
        var guid = Guid.NewGuid().ToString();
        new DeviceId(guid).Should().Be(new DeviceId(guid));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "DeviceMfaValueObjectTests"
```
Expected: compile error (type not found).

- [ ] **Step 3: Implement `DeviceId`**

```csharp
// src/SocialDDD.Domain/Users/DeviceId.cs
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record DeviceId
{
    public string Value { get; }

    public DeviceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException("DeviceId must not be empty.");
        if (!Guid.TryParse(value, out _))
            throw new DomainValidationException("DeviceId must be a valid GUID.");
        Value = value;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "DeviceMfaValueObjectTests"
```
Expected: only the DeviceId tests run and pass (OTP tests not yet written).

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Domain/Users/DeviceId.cs tests/SocialDDD.Domain.Tests/DeviceMfaValueObjectTests.cs
git commit -m "feat: add DeviceId value object"
```

---

## Task 2: `OneTimePasscode` value object

**Files:**
- Create: `src/SocialDDD.Domain/Users/OneTimePasscode.cs`
- Modify: `tests/SocialDDD.Domain.Tests/DeviceMfaValueObjectTests.cs`

- [ ] **Step 1: Add OTP tests to `DeviceMfaValueObjectTests.cs`**

Append these facts inside the class (after the DeviceId tests):

```csharp
    // ---- OneTimePasscode ----

    [Fact]
    public void OneTimePasscode_ValidCode_Succeeds()
    {
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));
        otp.Code.Should().Be("123456");
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("12345")]   // too short
    [InlineData("1234567")] // too long
    [InlineData("12345A")]  // non-digit
    public void OneTimePasscode_InvalidCode_ThrowsDomainValidationException(string input)
    {
        var act = () => new OneTimePasscode(input, DateTimeOffset.UtcNow.AddMinutes(10));
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void OneTimePasscode_NotExpired_IsExpiredReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("654321", now.AddMinutes(10));
        otp.IsExpired(now).Should().BeFalse();
    }

    [Fact]
    public void OneTimePasscode_Expired_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("654321", now.AddMinutes(-1));
        otp.IsExpired(now).Should().BeTrue();
    }

    [Fact]
    public void OneTimePasscode_ExpiresExactlyNow_IsExpiredReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var otp = new OneTimePasscode("654321", now);
        otp.IsExpired(now).Should().BeTrue();
    }
```

- [ ] **Step 2: Run to verify new tests fail**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "DeviceMfaValueObjectTests"
```
Expected: compile error (OneTimePasscode not found).

- [ ] **Step 3: Implement `OneTimePasscode`**

```csharp
// src/SocialDDD.Domain/Users/OneTimePasscode.cs
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record OneTimePasscode
{
    public string Code { get; }
    public DateTimeOffset ExpiresAt { get; }

    public OneTimePasscode(string code, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(code))
            throw new DomainValidationException("OTP must not be empty.");
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            throw new DomainValidationException("OTP must be exactly 6 digits.");
        Code = code;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "DeviceMfaValueObjectTests"
```
Expected: all tests pass.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Domain/Users/OneTimePasscode.cs tests/SocialDDD.Domain.Tests/DeviceMfaValueObjectTests.cs
git commit -m "feat: add OneTimePasscode value object"
```

---

## Task 3: Domain repository interfaces

**Files:**
- Create: `src/SocialDDD.Domain/Users/IRememberedDeviceRepository.cs`
- Create: `src/SocialDDD.Domain/Users/IOtpRepository.cs`

- [ ] **Step 1: Create `IRememberedDeviceRepository`**

```csharp
// src/SocialDDD.Domain/Users/IRememberedDeviceRepository.cs
namespace SocialDDD.Domain.Users;

public interface IRememberedDeviceRepository
{
    Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
    Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create `IOtpRepository`**

```csharp
// src/SocialDDD.Domain/Users/IOtpRepository.cs
namespace SocialDDD.Domain.Users;

public interface IOtpRepository
{
    Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default);
    Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
    Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build src/SocialDDD.Domain
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Domain/Users/IRememberedDeviceRepository.cs src/SocialDDD.Domain/Users/IOtpRepository.cs
git commit -m "feat: add IRememberedDeviceRepository and IOtpRepository interfaces"
```

---

## Task 4: Update `IEmailService` and email implementations

**Files:**
- Modify: `src/SocialDDD.Application/Interfaces/IEmailService.cs`
- Modify: `src/SocialDDD.Infrastructure/Emails/ConsoleEmailService.cs`
- Modify: `src/SocialDDD.Infrastructure/Emails/AzureCommunicationEmailService.cs`

- [ ] **Step 1: Add `SendOtpEmailAsync` to `IEmailService`**

Open `src/SocialDDD.Application/Interfaces/IEmailService.cs`. Replace the contents with:

```csharp
namespace SocialDDD.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default);
    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
}
```

- [ ] **Step 2: Implement in `ConsoleEmailService`**

Open `src/SocialDDD.Infrastructure/Emails/ConsoleEmailService.cs`. Replace the contents with:

```csharp
using Microsoft.Extensions.Logging;
using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Emails;

internal sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
    {
        logger.LogInformation("Verification email to {Email}: code = {Code}", toEmail, code);
        return Task.CompletedTask;
    }

    public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
    {
        logger.LogInformation("OTP email to {Email}: otp = {Otp}", toEmail, otp);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Implement in `AzureCommunicationEmailService`**

Read the current file first, then add the `SendOtpEmailAsync` method following the same pattern as `SendVerificationEmailAsync` (send a simple email with subject "Your login code" and the OTP in the body).

The current `AzureCommunicationEmailService` sends via Azure Email SDK. Add:

```csharp
public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
{
    // Reuse the same send pattern as SendVerificationEmailAsync but with OTP subject/body.
    return SendEmailAsync(
        toEmail,
        subject: "Your SocialDDD login code",
        plainText: $"Your one-time login code is: {otp}\n\nThis code expires in 10 minutes.",
        ct);
}
```

NOTE: Read `AzureCommunicationEmailService.cs` before editing to understand the private helper method signature.

- [ ] **Step 4: Build to verify no errors**

```
dotnet build src/SocialDDD.Infrastructure
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Application/Interfaces/IEmailService.cs src/SocialDDD.Infrastructure/Emails/ConsoleEmailService.cs src/SocialDDD.Infrastructure/Emails/AzureCommunicationEmailService.cs
git commit -m "feat: add SendOtpEmailAsync to IEmailService and email implementations"
```

---

## Task 5: DTOs for new API endpoints

**Files:**
- Create: `src/SocialDDD.Application/Users/DTOs/LoginWithDeviceRequest.cs`
- Create: `src/SocialDDD.Application/Users/DTOs/VerifyDeviceOtpRequest.cs`

- [ ] **Step 1: Create `LoginWithDeviceRequest`**

```csharp
// src/SocialDDD.Application/Users/DTOs/LoginWithDeviceRequest.cs
namespace SocialDDD.Application.Users.DTOs;

public sealed record LoginWithDeviceRequest(string Email, string Password, string DeviceId);
```

- [ ] **Step 2: Create `VerifyDeviceOtpRequest`**

```csharp
// src/SocialDDD.Application/Users/DTOs/VerifyDeviceOtpRequest.cs
namespace SocialDDD.Application.Users.DTOs;

public sealed record VerifyDeviceOtpRequest(string Email, string DeviceId, string Otp, bool RememberDevice);
```

- [ ] **Step 3: Build to verify**

```
dotnet build src/SocialDDD.Application
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Application/Users/DTOs/LoginWithDeviceRequest.cs src/SocialDDD.Application/Users/DTOs/VerifyDeviceOtpRequest.cs
git commit -m "feat: add LoginWithDeviceRequest and VerifyDeviceOtpRequest DTOs"
```

---

## Task 6: `LoginWithDeviceCommand` handler

**Files:**
- Create: `src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs`
- Create: `tests/SocialDDD.Domain.Tests/LoginWithDeviceHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

```csharp
// tests/SocialDDD.Domain.Tests/LoginWithDeviceHandlerTests.cs
using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class LoginWithDeviceHandlerTests
{
    private static readonly string ValidDeviceId = Guid.NewGuid().ToString();

    private static User MakeActiveUser(string emailValue = "alice@example.com", string password = "hashed")
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email(emailValue),
            new PasswordHash(password),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    [Fact]
    public async Task Execute_KnownDevice_ReturnsSuccessWithToken()
    {
        var user = MakeActiveUser(password: "hash");
        var userRepo = new FakeUserRepo(user);
        var passwordHasher = new FakePasswordHasher(true);
        var tokenService = new FakeTokenService();
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: true);
        var otpRepo = new FakeOtpRepo();
        var emailService = new FakeEmailService();

        var handler = new LoginWithDeviceCommand(userRepo, passwordHasher, tokenService, deviceRepo, otpRepo, emailService);
        var result = await handler.ExecuteAsync(new LoginWithDeviceRequest("alice@example.com", "password", ValidDeviceId));

        result.Should().BeOfType<LoginWithDeviceResult.Success>();
        ((LoginWithDeviceResult.Success)result).Token.Should().Be("fake-token");
        emailService.OtpSent.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_UnknownDevice_ReturnsOtpRequiredAndSendsEmail()
    {
        var user = MakeActiveUser(password: "hash");
        var userRepo = new FakeUserRepo(user);
        var passwordHasher = new FakePasswordHasher(true);
        var tokenService = new FakeTokenService();
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var otpRepo = new FakeOtpRepo();
        var emailService = new FakeEmailService();

        var handler = new LoginWithDeviceCommand(userRepo, passwordHasher, tokenService, deviceRepo, otpRepo, emailService);
        var result = await handler.ExecuteAsync(new LoginWithDeviceRequest("alice@example.com", "password", ValidDeviceId));

        result.Should().BeOfType<LoginWithDeviceResult.OtpRequired>();
        emailService.OtpSent.Should().BeTrue();
        otpRepo.Saved.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_WrongPassword_ThrowsDomainException()
    {
        var user = MakeActiveUser(password: "hash");
        var userRepo = new FakeUserRepo(user);
        var passwordHasher = new FakePasswordHasher(false); // password check fails
        var tokenService = new FakeTokenService();
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var otpRepo = new FakeOtpRepo();
        var emailService = new FakeEmailService();

        var handler = new LoginWithDeviceCommand(userRepo, passwordHasher, tokenService, deviceRepo, otpRepo, emailService);
        var act = () => handler.ExecuteAsync(new LoginWithDeviceRequest("alice@example.com", "wrong", ValidDeviceId));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Invalid*");
    }

    [Fact]
    public async Task Execute_PendingUser_ThrowsDomainException()
    {
        var user = User.Register(
            new Username("bob"),
            new Email("bob@example.com"),
            new PasswordHash("hash"),
            new Handle("bob"),
            new DisplayName("Bob"));
        user.PopDomainEvents();

        var userRepo = new FakeUserRepo(user);
        var passwordHasher = new FakePasswordHasher(true);
        var tokenService = new FakeTokenService();
        var deviceRepo = new FakeRememberedDeviceRepo(isRemembered: false);
        var otpRepo = new FakeOtpRepo();
        var emailService = new FakeEmailService();

        var handler = new LoginWithDeviceCommand(userRepo, passwordHasher, tokenService, deviceRepo, otpRepo, emailService);
        var act = () => handler.ExecuteAsync(new LoginWithDeviceRequest("bob@example.com", "password", ValidDeviceId));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*verified*");
    }

    // ---- Fakes ----

    private sealed class FakeUserRepo(User? user) : IUserRepository
    {
        public Task AddAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user?.Id == id ? user : null);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(user?.Email == email ? user : null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(user?.Username == username ? user : null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user?.Handle == handle ? user : null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(user?.Email == email);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(user?.Username == username);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user?.Id == id);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user?.Handle == handle);
        public Task UpdateAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher(bool result) : IPasswordHasher
    {
        public string Hash(string password) => "hash";
        public bool Verify(string password, string hash) => result;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateToken(User user) => "fake-token";
    }

    private sealed class FakeRememberedDeviceRepo(bool isRemembered) : IRememberedDeviceRepository
    {
        public bool Remembered { get; private set; }
        public Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
            => Task.FromResult(isRemembered);
        public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
        {
            Remembered = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOtpRepo : IOtpRepository
    {
        public bool Saved { get; private set; }
        public bool Deleted { get; private set; }
        private OneTimePasscode? _stored;

        public Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default)
        {
            Saved = true;
            _stored = otp;
            return Task.CompletedTask;
        }
        public Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
            => Task.FromResult(_stored);
        public Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
        {
            Deleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool OtpSent { get; private set; }
        public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default)
        {
            OtpSent = true;
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run to verify compile failure**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "LoginWithDeviceHandlerTests"
```
Expected: compile error (LoginWithDeviceCommand, LoginWithDeviceResult not found).

- [ ] **Step 3: Implement `LoginWithDeviceCommand`**

```csharp
// src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs
using System.Security.Cryptography;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public abstract class LoginWithDeviceResult
{
    public sealed class Success(string token) : LoginWithDeviceResult
    {
        public string Token { get; } = token;
    }

    public sealed class OtpRequired : LoginWithDeviceResult { }
}

public sealed class LoginWithDeviceCommand(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IRememberedDeviceRepository deviceRepository,
    IOtpRepository otpRepository,
    IEmailService emailService)
{
    public async Task<LoginWithDeviceResult> ExecuteAsync(LoginWithDeviceRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var deviceId = new DeviceId(request.DeviceId);

        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainException("Invalid credentials.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash.Value))
            throw new DomainException("Invalid credentials.");

        if (user.Status == UserStatus.Pending)
            throw new DomainException("Account is not yet verified. Please check your email.");

        if (await deviceRepository.IsRememberedAsync(user.Id, deviceId, ct))
            return new LoginWithDeviceResult.Success(tokenService.GenerateToken(user));

        var otp = new OneTimePasscode(GenerateOtp(), DateTimeOffset.UtcNow.AddMinutes(10));
        await otpRepository.SaveAsync(user.Id, deviceId, otp, ct);
        await emailService.SendOtpEmailAsync(user.Email.Value, otp.Code, ct);

        return new LoginWithDeviceResult.OtpRequired();
    }

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "LoginWithDeviceHandlerTests"
```
Expected: all 4 tests pass.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs tests/SocialDDD.Domain.Tests/LoginWithDeviceHandlerTests.cs
git commit -m "feat: add LoginWithDeviceCommand handler with device-check and OTP flow"
```

---

## Task 7: `VerifyDeviceOtpCommand` handler

**Files:**
- Create: `src/SocialDDD.Application/Users/Commands/VerifyDeviceOtpCommand.cs`
- Create: `tests/SocialDDD.Domain.Tests/VerifyDeviceOtpHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

```csharp
// tests/SocialDDD.Domain.Tests/VerifyDeviceOtpHandlerTests.cs
using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class VerifyDeviceOtpHandlerTests
{
    private static readonly string ValidDeviceId = Guid.NewGuid().ToString();

    private static User MakeActiveUser(string emailValue = "alice@example.com")
    {
        var user = User.RegisterImmediate(
            new Username("alice"),
            new Email(emailValue),
            new PasswordHash("hash"),
            new Handle("alice"),
            new DisplayName("Alice"));
        user.PopDomainEvents();
        return user;
    }

    [Fact]
    public async Task Execute_CorrectOtp_ReturnsToken()
    {
        var user = MakeActiveUser();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, new DeviceId(ValidDeviceId), otp);
        var deviceRepo = new FakeRememberedDeviceRepo();
        var tokenService = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenService);
        var result = await handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", ValidDeviceId, "123456", false));

        result.Token.Should().Be("fake-token");
        otpRepo.Deleted.Should().BeTrue();
        deviceRepo.Remembered.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_CorrectOtpWithRememberDevice_RemembersDevice()
    {
        var user = MakeActiveUser();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, new DeviceId(ValidDeviceId), otp);
        var deviceRepo = new FakeRememberedDeviceRepo();
        var tokenService = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenService);
        var result = await handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", ValidDeviceId, "123456", true));

        result.Token.Should().Be("fake-token");
        deviceRepo.Remembered.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_WrongOtp_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(10));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, new DeviceId(ValidDeviceId), otp);
        var deviceRepo = new FakeRememberedDeviceRepo();
        var tokenService = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenService);
        var act = () => handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", ValidDeviceId, "000000", false));

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*Invalid*");
    }

    [Fact]
    public async Task Execute_ExpiredOtp_ThrowsDomainValidationException()
    {
        var user = MakeActiveUser();
        var otp = new OneTimePasscode("123456", DateTimeOffset.UtcNow.AddMinutes(-1));

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(user.Id, new DeviceId(ValidDeviceId), otp);
        var deviceRepo = new FakeRememberedDeviceRepo();
        var tokenService = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenService);
        var act = () => handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", ValidDeviceId, "123456", false));

        await act.Should().ThrowAsync<DomainValidationException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task Execute_NoOtpFound_ThrowsDomainException()
    {
        var user = MakeActiveUser();

        var userRepo = new FakeUserRepo(user);
        var otpRepo = new FakeOtpRepo(null, null, null); // no OTP stored
        var deviceRepo = new FakeRememberedDeviceRepo();
        var tokenService = new FakeTokenService();

        var handler = new VerifyDeviceOtpCommand(userRepo, otpRepo, deviceRepo, tokenService);
        var act = () => handler.ExecuteAsync(new VerifyDeviceOtpRequest("alice@example.com", ValidDeviceId, "123456", false));

        await act.Should().ThrowAsync<DomainException>();
    }

    // ---- Fakes ----

    private sealed class FakeUserRepo(User? user) : IUserRepository
    {
        public Task AddAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
        public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user?.Id == id ? user : null);
        public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(user?.Email == email ? user : null);
        public Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(user?.Username == username ? user : null);
        public Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user?.Handle == handle ? user : null);
        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(user?.Email == email);
        public Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) => Task.FromResult(user?.Username == username);
        public Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) => Task.FromResult(user?.Id == id);
        public Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) => Task.FromResult(user?.Handle == handle);
        public Task UpdateAsync(User u, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeOtpRepo(UserId? userId, DeviceId? deviceId, OneTimePasscode? otp) : IOtpRepository
    {
        public bool Deleted { get; private set; }

        public Task SaveAsync(UserId uid, DeviceId did, OneTimePasscode o, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<OneTimePasscode?> FindAsync(UserId uid, DeviceId did, CancellationToken ct = default)
            => Task.FromResult(userId == uid && deviceId == did ? otp : null);

        public Task DeleteAsync(UserId uid, DeviceId did, CancellationToken ct = default)
        {
            Deleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRememberedDeviceRepo : IRememberedDeviceRepository
    {
        public bool Remembered { get; private set; }
        public Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
        {
            Remembered = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateToken(User user) => "fake-token";
    }
}
```

- [ ] **Step 2: Run to verify compile failure**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "VerifyDeviceOtpHandlerTests"
```
Expected: compile error (VerifyDeviceOtpCommand not found).

- [ ] **Step 3: Implement `VerifyDeviceOtpCommand`**

```csharp
// src/SocialDDD.Application/Users/Commands/VerifyDeviceOtpCommand.cs
using System.Security.Cryptography;
using System.Text;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class VerifyDeviceOtpCommand(
    IUserRepository userRepository,
    IOtpRepository otpRepository,
    IRememberedDeviceRepository deviceRepository,
    ITokenService tokenService)
{
    public async Task<TokenResponse> ExecuteAsync(VerifyDeviceOtpRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var deviceId = new DeviceId(request.DeviceId);

        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainException("User not found.");

        var stored = await otpRepository.FindAsync(user.Id, deviceId, ct)
            ?? throw new DomainException("No OTP found. Please request a new login code.");

        if (stored.IsExpired(DateTimeOffset.UtcNow))
            throw new DomainValidationException("OTP has expired. Please request a new login code.");

        var storedBytes = Encoding.UTF8.GetBytes(stored.Code);
        var providedBytes = Encoding.UTF8.GetBytes(request.Otp);
        if (!CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes))
            throw new DomainValidationException("Invalid OTP.");

        await otpRepository.DeleteAsync(user.Id, deviceId, ct);

        if (request.RememberDevice)
            await deviceRepository.RememberAsync(user.Id, deviceId, ct);

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "VerifyDeviceOtpHandlerTests"
```
Expected: all 5 tests pass.

- [ ] **Step 5: Run full test suite**

```
dotnet test tests/SocialDDD.Domain.Tests
```
Expected: all existing tests still pass.

- [ ] **Step 6: Commit**

```
git add src/SocialDDD.Application/Users/Commands/VerifyDeviceOtpCommand.cs tests/SocialDDD.Domain.Tests/VerifyDeviceOtpHandlerTests.cs
git commit -m "feat: add VerifyDeviceOtpCommand handler"
```

---

## Task 8: Infrastructure — InMemory repositories

**Files:**
- Create: `src/SocialDDD.Infrastructure/Persistence/Devices/InMemoryRememberedDeviceRepository.cs`
- Create: `src/SocialDDD.Infrastructure/Persistence/Otps/InMemoryOtpRepository.cs`

- [ ] **Step 1: Create `InMemoryRememberedDeviceRepository`**

```csharp
// src/SocialDDD.Infrastructure/Persistence/Devices/InMemoryRememberedDeviceRepository.cs
using System.Collections.Concurrent;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Devices;

internal sealed class InMemoryRememberedDeviceRepository : IRememberedDeviceRepository
{
    private static readonly ConcurrentDictionary<string, bool> _store = new();

    private static string Key(UserId userId, DeviceId deviceId)
        => $"{userId.Value}::{deviceId.Value}";

    public Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
        => Task.FromResult(_store.ContainsKey(Key(userId, deviceId)));

    public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        _store[Key(userId, deviceId)] = true;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Create `InMemoryOtpRepository`**

```csharp
// src/SocialDDD.Infrastructure/Persistence/Otps/InMemoryOtpRepository.cs
using System.Collections.Concurrent;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Otps;

internal sealed class InMemoryOtpRepository : IOtpRepository
{
    private static readonly ConcurrentDictionary<string, OneTimePasscode> _store = new();

    private static string Key(UserId userId, DeviceId deviceId)
        => $"{userId.Value}::{deviceId.Value}";

    public Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default)
    {
        _store[Key(userId, deviceId)] = otp;
        return Task.CompletedTask;
    }

    public Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        _store.TryGetValue(Key(userId, deviceId), out var otp);
        return Task.FromResult(otp);
    }

    public Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        _store.TryRemove(Key(userId, deviceId), out _);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Build to verify**

```
dotnet build src/SocialDDD.Infrastructure
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Infrastructure/Persistence/Devices/InMemoryRememberedDeviceRepository.cs src/SocialDDD.Infrastructure/Persistence/Otps/InMemoryOtpRepository.cs
git commit -m "feat: add InMemory implementations for device and OTP repositories"
```

---

## Task 9: Infrastructure — MongoDB repositories

**Files:**
- Create: `src/SocialDDD.Infrastructure/Persistence/Devices/MongoDbRememberedDeviceRepository.cs`
- Create: `src/SocialDDD.Infrastructure/Persistence/Otps/MongoDbOtpRepository.cs`
- Modify: `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs`

- [ ] **Step 1: Create `MongoDbRememberedDeviceRepository`**

```csharp
// src/SocialDDD.Infrastructure/Persistence/Devices/MongoDbRememberedDeviceRepository.cs
using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Devices;

internal sealed class MongoDbRememberedDeviceRepository(MongoDbContext context) : IRememberedDeviceRepository
{
    public async Task<bool> IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var count = await context.RememberedDevices
            .CountDocumentsAsync(d => d.UserId == userId.Value.ToString() && d.DeviceId == deviceId.Value, cancellationToken: ct);
        return count > 0;
    }

    public Task RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var doc = new RememberedDeviceDocument(
            userId.Value.ToString(),
            deviceId.Value,
            DateTime.UtcNow);
        return context.RememberedDevices.ReplaceOneAsync(
            d => d.UserId == doc.UserId && d.DeviceId == doc.DeviceId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }
}

internal sealed class RememberedDeviceDocument(string userId, string deviceId, DateTime rememberedAt)
{
    public string UserId { get; init; } = userId;
    public string DeviceId { get; init; } = deviceId;
    public DateTime RememberedAt { get; init; } = rememberedAt;
}
```

- [ ] **Step 2: Create `MongoDbOtpRepository`**

```csharp
// src/SocialDDD.Infrastructure/Persistence/Otps/MongoDbOtpRepository.cs
using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Otps;

internal sealed class MongoDbOtpRepository(MongoDbContext context) : IOtpRepository
{
    public Task SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct = default)
    {
        var doc = new OtpDocument(
            userId.Value.ToString(),
            deviceId.Value,
            otp.Code,
            otp.ExpiresAt.UtcDateTime);
        return context.DeviceOtps.ReplaceOneAsync(
            d => d.UserId == doc.UserId && d.DeviceId == doc.DeviceId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<OneTimePasscode?> FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var userKey = userId.Value.ToString();
        var deviceKey = deviceId.Value;
        var doc = await context.DeviceOtps
            .Find(d => d.UserId == userKey && d.DeviceId == deviceKey)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return null;
        return new OneTimePasscode(doc.Code, new DateTimeOffset(doc.ExpiresAt, TimeSpan.Zero));
    }

    public Task DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct = default)
    {
        var userKey = userId.Value.ToString();
        var deviceKey = deviceId.Value;
        return context.DeviceOtps.DeleteOneAsync(
            d => d.UserId == userKey && d.DeviceId == deviceKey,
            ct);
    }
}

internal sealed class OtpDocument(string userId, string deviceId, string code, DateTime expiresAt)
{
    public string UserId { get; init; } = userId;
    public string DeviceId { get; init; } = deviceId;
    public string Code { get; init; } = code;
    public DateTime ExpiresAt { get; init; } = expiresAt;
}
```

- [ ] **Step 3: Add collections and indexes to `MongoDbContext`**

Open `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs`. Add the two new collection properties and their index creation in `EnsureIndexes()`. The file should look like this after editing:

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Persistence.Devices;
using SocialDDD.Infrastructure.Persistence.Mapping;
using SocialDDD.Infrastructure.Persistence.Otps;
using SocialDDD.Infrastructure.Persistence.VerificationCodes;

namespace SocialDDD.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoSettings> options)
    {
        BsonMappings.Register();

        var client = new MongoClient(options.Value.ConnectionString);
        _database = client.GetDatabase(options.Value.DatabaseName);

        EnsureIndexes();
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Post> Posts => _database.GetCollection<Post>("posts");
    internal IMongoCollection<VerificationCodeDocument> VerificationCodes =>
        _database.GetCollection<VerificationCodeDocument>("verification_codes");
    internal IMongoCollection<RememberedDeviceDocument> RememberedDevices =>
        _database.GetCollection<RememberedDeviceDocument>("remembered_devices");
    internal IMongoCollection<OtpDocument> DeviceOtps =>
        _database.GetCollection<OtpDocument>("device_otps");

    private void EnsureIndexes()
    {
        var handleIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending("handle"),
            new CreateIndexOptions { Unique = true, Background = true, Name = "handle_unique" });
        Users.Indexes.CreateOne(handleIndex);

        var ttlVerificationIndex = new CreateIndexModel<VerificationCodeDocument>(
            Builders<VerificationCodeDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_ttl" });
        VerificationCodes.Indexes.CreateOne(ttlVerificationIndex);

        var deviceIndex = new CreateIndexModel<RememberedDeviceDocument>(
            Builders<RememberedDeviceDocument>.IndexKeys
                .Ascending(d => d.UserId)
                .Ascending(d => d.DeviceId),
            new CreateIndexOptions { Unique = true, Background = true, Name = "userId_deviceId_unique" });
        RememberedDevices.Indexes.CreateOne(deviceIndex);

        var otpTtlIndex = new CreateIndexModel<OtpDocument>(
            Builders<OtpDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "otp_expiresAt_ttl" });
        DeviceOtps.Indexes.CreateOne(otpTtlIndex);
    }
}
```

- [ ] **Step 4: Build to verify**

```
dotnet build src/SocialDDD.Infrastructure
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Infrastructure/Persistence/Devices/MongoDbRememberedDeviceRepository.cs src/SocialDDD.Infrastructure/Persistence/Otps/MongoDbOtpRepository.cs src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs
git commit -m "feat: add MongoDB implementations for device and OTP repositories"
```

---

## Task 10: Register in DependencyInjection

**Files:**
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Update `DependencyInjection.cs`**

Replace the file contents with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Auth;
using SocialDDD.Infrastructure.Emails;
using SocialDDD.Infrastructure.Events;
using SocialDDD.Infrastructure.Persistence;
using SocialDDD.Infrastructure.Persistence.Devices;
using SocialDDD.Infrastructure.Persistence.Otps;
using SocialDDD.Infrastructure.Persistence.Posts;
using SocialDDD.Infrastructure.Persistence.Users;
using SocialDDD.Infrastructure.Persistence.VerificationCodes;

namespace SocialDDD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoSettings>(configuration.GetSection("Mongo"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPostRepository, PostRepository>();

        // Verification code repository: "MongoDb" or "InMemory" (default)
        var verificationCodeRepo = configuration["Features:EmailVerificationRepository"] ?? "InMemory";
        if (verificationCodeRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IVerificationCodeRepository, MongoDbVerificationCodeRepository>();
        else
            services.AddScoped<IVerificationCodeRepository, InMemoryVerificationCodeRepository>();

        // Remembered device repository: "MongoDb" or "InMemory" (default)
        var deviceRepo = configuration["Features:RememberedDeviceRepository"] ?? "InMemory";
        if (deviceRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IRememberedDeviceRepository, MongoDbRememberedDeviceRepository>();
        else
            services.AddScoped<IRememberedDeviceRepository, InMemoryRememberedDeviceRepository>();

        // OTP repository: "MongoDb" or "InMemory" (default)
        var otpRepo = configuration["Features:OtpRepository"] ?? "InMemory";
        if (otpRepo.Equals("MongoDb", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IOtpRepository, MongoDbOtpRepository>();
        else
            services.AddScoped<IOtpRepository, InMemoryOtpRepository>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Email service: "AzureCommunication" or "Console" (default)
        var emailService = configuration["Features:EmailService"] ?? "Console";
        if (emailService.Equals("AzureCommunication", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailService, AzureCommunicationEmailService>();
        else
            services.AddScoped<IEmailService, ConsoleEmailService>();

        services.AddScoped<RegisterPendingUserCommand>();
        services.AddScoped<VerifyRegistrationCommand>();
        services.AddScoped<LoginWithDeviceCommand>();
        services.AddScoped<VerifyDeviceOtpCommand>();

        return services;
    }
}
```

- [ ] **Step 2: Build entire solution**

```
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/SocialDDD.Infrastructure/DependencyInjection.cs
git commit -m "feat: register device and OTP repositories and commands in DI"
```

---

## Task 11: API — `SessionsController`

**Files:**
- Create: `src/SocialDDD.Api/Controllers/SessionsController.cs`

- [ ] **Step 1: Create `SessionsController`**

```csharp
// src/SocialDDD.Api/Controllers/SessionsController.cs
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    LoginWithDeviceCommand loginCommand,
    VerifyDeviceOtpCommand verifyCommand) : ControllerBase
{
    /// <summary>
    /// POST /api/sessions/device
    /// Body: { email, password, deviceId }
    /// Returns 200 with token if device is remembered; 202 Accepted if OTP was sent; 401 on bad credentials.
    /// </summary>
    [HttpPost("device")]
    public async Task<IActionResult> LoginWithDevice([FromBody] LoginWithDeviceRequest request, CancellationToken ct)
    {
        try
        {
            var result = await loginCommand.ExecuteAsync(request, ct);
            return result switch
            {
                LoginWithDeviceResult.Success s => Ok(new TokenResponse(s.Token, Guid.Empty, string.Empty)),
                LoginWithDeviceResult.OtpRequired => Accepted(),
                _ => StatusCode(500)
            };
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException) { return Unauthorized(new { error = "Invalid credentials." }); }
    }

    /// <summary>
    /// POST /api/sessions/device/verify
    /// Body: { email, deviceId, otp, rememberDevice }
    /// Returns 200 with token on success; 400 on wrong/expired OTP.
    /// </summary>
    [HttpPost("device/verify")]
    public async Task<IActionResult> VerifyDeviceOtp([FromBody] VerifyDeviceOtpRequest request, CancellationToken ct)
    {
        try
        {
            var response = await verifyCommand.ExecuteAsync(request, ct);
            return Ok(response);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
```

NOTE: The `LoginWithDevice` known-device path needs the real user info in `TokenResponse`. Update the handler to also return `UserId` and `Username` in the `Success` result. See the note in the self-review section below — Task 11 includes this correction.

**Correction — update `LoginWithDeviceResult.Success` to carry userId and username:**

In `src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs`, change:

```csharp
public sealed class Success(string token) : LoginWithDeviceResult
{
    public string Token { get; } = token;
}
```

to:

```csharp
public sealed class Success(string token, Guid userId, string username) : LoginWithDeviceResult
{
    public string Token { get; } = token;
    public Guid UserId { get; } = userId;
    public string Username { get; } = username;
}
```

And update the handler's return line from:

```csharp
return new LoginWithDeviceResult.Success(tokenService.GenerateToken(user));
```

to:

```csharp
return new LoginWithDeviceResult.Success(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
```

Then in `SessionsController`, update the known-device branch to:

```csharp
LoginWithDeviceResult.Success s => Ok(new TokenResponse(s.Token, s.UserId, s.Username)),
```

Also update `LoginWithDeviceHandlerTests.cs` — the Success assertion should still only check the token, which is fine since `UserId` and `Username` are just additional data. No test changes needed beyond the compile fix (the fake user is built with `alice` username and a known `Guid`, but the test only checks `.Token` so it still passes once `LoginWithDeviceResult.Success` constructor is updated).

- [ ] **Step 2: Build entire solution**

```
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run all tests**

```
dotnet test
```
Expected: all tests pass.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Api/Controllers/SessionsController.cs src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs
git commit -m "feat: add SessionsController with device login and OTP verification endpoints"
```

---

## Task 12: Final verification and feature commit

- [ ] **Step 1: Run full build**

```
dotnet build
```
Expected: 0 errors, 0 warnings related to new code.

- [ ] **Step 2: Run all tests**

```
dotnet test
```
Expected: all tests pass (existing + new).

- [ ] **Step 3: Create final feature commit**

```
git commit --allow-empty -m "feat: add device-based MFA login with OTP (prompt 03)"
```

(Only needed if previous task commits are already made. Skip if you want to squash.)

---

## Self-Review Checklist

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| `DeviceId` value object (non-empty GUID string) | Task 1 |
| `OneTimePasscode` value object (6-digit, expiry, `IsExpired`) | Task 2 |
| `IRememberedDeviceRepository` with `IsRememberedAsync` / `RememberAsync` | Task 3 |
| `IOtpRepository` with `SaveAsync` / `FindAsync` / `DeleteAsync` | Task 3 |
| `LoginWithDeviceCommand` — credential check, device check, OTP generation | Task 6 |
| `VerifyDeviceOtpCommand` — OTP validation, delete, remember, token | Task 7 |
| `SendOtpEmailAsync` on `IEmailService` | Task 4 |
| `InMemoryRememberedDeviceRepository` | Task 8 |
| `MongoDbRememberedDeviceRepository` (collection, compound unique index) | Task 9 |
| `InMemoryOtpRepository` | Task 8 |
| `MongoDbOtpRepository` (collection, TTL index) | Task 9 |
| DI registration (InMemory default, config-based swap) | Task 10 |
| `POST /api/sessions/device` — 200/202/401 | Task 11 |
| `POST /api/sessions/device/verify` — 200/400 | Task 11 |
| Unit tests: `OneTimePasscode` expiry + validation | Task 2 |
| Unit tests: `DeviceId` validation | Task 1 |
| Unit tests: login handler (known device → Success, unknown → OtpRequired) | Task 6 |
| Unit tests: verify handler (wrong OTP, expired OTP, correct OTP, remember device) | Task 7 |
| BsonMappings update | Not needed — `DeviceId` and `OneTimePasscode` are not mapped as BSON class properties; documents use plain strings |
| `POST /api/sessions` unchanged | Not modified |

**Placeholder scan:** No TBD/TODO/placeholder text found.

**Type consistency:**
- `LoginWithDeviceResult.Success(string token, Guid userId, string username)` used consistently in Task 6 and Task 11.
- `IOtpRepository.FindAsync` returns `Task<OneTimePasscode?>` — matches usage in Task 7.
- `IRememberedDeviceRepository.RememberAsync` is `void` (returns `Task`) — matches usage in Task 7.
- `DeviceId` constructor signature matches across all tasks.
- `OneTimePasscode` constructor `(string code, DateTimeOffset expiresAt)` matches across all tasks.
