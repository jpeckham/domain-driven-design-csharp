# Email Verification Registration Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a two-step email verification registration flow where accounts start Pending, receive a verification code by email, and become Active after submitting the code.

**Architecture:** `User` gets a `Status` property (`Pending`/`Active`). A `VerificationCode` value object is stored in a separate repository. Two new application-layer commands handle "start registration" and "verify code". The existing `POST /api/accounts` immediate-creation path is preserved unchanged.

**Tech Stack:** .NET 9, C#, xUnit + FluentAssertions, MongoDB (in-memory for tests), existing DDD infrastructure patterns.

---

## File Map

### Domain (`SocialDDD.Domain`)
- Create: `src/SocialDDD.Domain/Users/UserStatus.cs` — `Pending`/`Active` enum
- Modify: `src/SocialDDD.Domain/Users/User.cs` — add `Status`, `Activate()`, `RegisterImmediate()` factory
- Create: `src/SocialDDD.Domain/Users/Events/UserActivated.cs` — domain event
- Create: `src/SocialDDD.Domain/Users/VerificationCode.cs` — value object (code string, ExpiresAt, IsExpired)
- Create: `src/SocialDDD.Domain/Users/IVerificationCodeRepository.cs` — repository interface

### Application (`SocialDDD.Application`)
- Create: `src/SocialDDD.Application/Interfaces/IEmailService.cs` — email interface
- Create: `src/SocialDDD.Application/Users/DTOs/RegisterPendingRequest.cs` — request DTO
- Create: `src/SocialDDD.Application/Users/DTOs/VerifyRegistrationRequest.cs` — request DTO
- Create: `src/SocialDDD.Application/Users/Commands/RegisterPendingUserCommand.cs` — handler
- Create: `src/SocialDDD.Application/Users/Commands/VerifyRegistrationCommand.cs` — handler
- Modify: `src/SocialDDD.Application/Users/UserService.cs` — reject Pending users in `LoginAsync`

### Infrastructure (`SocialDDD.Infrastructure`)
- Create: `src/SocialDDD.Infrastructure/Persistence/VerificationCodes/InMemoryVerificationCodeRepository.cs`
- Create: `src/SocialDDD.Infrastructure/Persistence/VerificationCodes/MongoDbVerificationCodeRepository.cs`
- Create: `src/SocialDDD.Infrastructure/Email/ConsoleEmailService.cs`
- Create: `src/SocialDDD.Infrastructure/Email/AzureCommunicationEmailService.cs` — stub
- Modify: `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs` — add `UserStatus` enum serialization note (enums serialize as strings via convention)
- Modify: `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs` — add `VerificationCodes` collection + TTL index
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs` — register new services

### API (`SocialDDD.Api`)
- Create: `src/SocialDDD.Api/Controllers/RegistrationsController.cs` — `POST /api/registrations`, `POST /api/registrations/verify`
- Modify: `src/SocialDDD.Api/Program.cs` — register new command handlers

### Tests (`tests/SocialDDD.Domain.Tests`)
- Modify: `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs` — add `VerificationCode` expiry tests
- Modify: `tests/SocialDDD.Domain.Tests/UserTests.cs` — add `Activate()` raises `UserActivated` event tests
- Create: `tests/SocialDDD.Domain.Tests/VerifyRegistrationHandlerTests.cs` — handler unit tests (expired, wrong code, correct code)

---

## Task 1: Domain — UserStatus enum and VerificationCode value object

**Files:**
- Create: `src/SocialDDD.Domain/Users/UserStatus.cs`
- Create: `src/SocialDDD.Domain/Users/VerificationCode.cs`

- [ ] **Step 1: Write failing tests for VerificationCode**

In `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs`, add at the end of the class:

```csharp
[Fact]
public void VerificationCode_NotExpired_IsExpiredReturnsFalse()
{
    var now = DateTimeOffset.UtcNow;
    var code = new VerificationCode("123456", now.AddMinutes(15));
    code.IsExpired(now).Should().BeFalse();
}

[Fact]
public void VerificationCode_Expired_IsExpiredReturnsTrue()
{
    var now = DateTimeOffset.UtcNow;
    var code = new VerificationCode("123456", now.AddMinutes(-1));
    code.IsExpired(now).Should().BeTrue();
}

[Fact]
public void VerificationCode_ExpiresExactlyNow_IsExpiredReturnsTrue()
{
    var now = DateTimeOffset.UtcNow;
    var code = new VerificationCode("123456", now);
    code.IsExpired(now).Should().BeTrue();
}
```

Also add the using at the top of the file if not present:
```csharp
// no extra using needed — VerificationCode is in SocialDDD.Domain.Users
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "VerificationCode" -v minimal
```
Expected: compile error (type not found).

- [ ] **Step 3: Create UserStatus enum**

Create `src/SocialDDD.Domain/Users/UserStatus.cs`:

```csharp
namespace SocialDDD.Domain.Users;

public enum UserStatus
{
    Pending,
    Active
}
```

- [ ] **Step 4: Create VerificationCode value object**

Create `src/SocialDDD.Domain/Users/VerificationCode.cs`:

```csharp
namespace SocialDDD.Domain.Users;

public sealed record VerificationCode(string Code, DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "VerificationCode" -v minimal
```
Expected: 3 tests PASS.

- [ ] **Step 6: Commit**

```
git add src/SocialDDD.Domain/Users/UserStatus.cs src/SocialDDD.Domain/Users/VerificationCode.cs tests/SocialDDD.Domain.Tests/ValueObjectTests.cs
git commit -m "feat: add UserStatus enum and VerificationCode value object"
```

---

## Task 2: Domain — UserActivated event and IVerificationCodeRepository

**Files:**
- Create: `src/SocialDDD.Domain/Users/Events/UserActivated.cs`
- Create: `src/SocialDDD.Domain/Users/IVerificationCodeRepository.cs`

- [ ] **Step 1: Create UserActivated domain event**

Create `src/SocialDDD.Domain/Users/Events/UserActivated.cs`:

```csharp
using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserActivated(UserId UserId) : IDomainEvent;
```

- [ ] **Step 2: Create IVerificationCodeRepository interface**

Create `src/SocialDDD.Domain/Users/IVerificationCodeRepository.cs`:

```csharp
namespace SocialDDD.Domain.Users;

public interface IVerificationCodeRepository
{
    Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default);
    Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task DeleteAsync(UserId userId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build to verify**

```
dotnet build src/SocialDDD.Domain
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Domain/Users/Events/UserActivated.cs src/SocialDDD.Domain/Users/IVerificationCodeRepository.cs
git commit -m "feat: add UserActivated event and IVerificationCodeRepository"
```

---

## Task 3: Domain — Update User aggregate with Status, Activate(), RegisterImmediate()

**Files:**
- Modify: `src/SocialDDD.Domain/Users/User.cs`

- [ ] **Step 1: Write failing tests**

Add to `tests/SocialDDD.Domain.Tests/UserTests.cs`:

```csharp
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

    user.PopDomainEvents(); // clear UserRegistered

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
```

Also add the using at the top of `UserTests.cs`:
```csharp
using SocialDDD.Domain.Users.Events;
```
(Already present — no change needed.)

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "Status|Activate|RegisterImmediate" -v minimal
```
Expected: compile errors (members not found).

- [ ] **Step 3: Update User aggregate**

Replace `src/SocialDDD.Domain/Users/User.cs` entirely:

```csharp
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Primitives;
using SocialDDD.Domain.Users.Events;

namespace SocialDDD.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    public Username Username { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public Handle Handle { get; private set; } = null!;
    public DisplayName DisplayName { get; private set; } = null!;
    public DateTime RegisteredAt { get; private set; }
    public UserStatus Status { get; private set; }

    private User() { }

    public static User Register(
        Username username,
        Email email,
        PasswordHash passwordHash,
        Handle handle,
        DisplayName displayName)
    {
        var user = new User
        {
            Id = UserId.New(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Handle = handle,
            DisplayName = displayName,
            RegisteredAt = DateTime.UtcNow,
            Status = UserStatus.Pending
        };
        user.RaiseDomainEvent(new UserRegistered(user.Id, handle, displayName));
        return user;
    }

    public static User RegisterImmediate(
        Username username,
        Email email,
        PasswordHash passwordHash,
        Handle handle,
        DisplayName displayName)
    {
        var user = new User
        {
            Id = UserId.New(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Handle = handle,
            DisplayName = displayName,
            RegisteredAt = DateTime.UtcNow,
            Status = UserStatus.Active
        };
        user.RaiseDomainEvent(new UserRegistered(user.Id, handle, displayName));
        return user;
    }

    public void Activate()
    {
        if (Status == UserStatus.Active)
            throw new DomainException("User is already active.");
        Status = UserStatus.Active;
        RaiseDomainEvent(new UserActivated(Id));
    }

    public void UpdateDisplayName(DisplayName newName) => DisplayName = newName;
}
```

- [ ] **Step 4: Run domain tests**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```
Expected: all tests PASS.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Domain/Users/User.cs tests/SocialDDD.Domain.Tests/UserTests.cs
git commit -m "feat: add Status property, Activate(), and RegisterImmediate() to User aggregate"
```

---

## Task 4: Application — IEmailService and new DTOs

**Files:**
- Create: `src/SocialDDD.Application/Interfaces/IEmailService.cs`
- Create: `src/SocialDDD.Application/Users/DTOs/RegisterPendingRequest.cs`
- Create: `src/SocialDDD.Application/Users/DTOs/VerifyRegistrationRequest.cs`

- [ ] **Step 1: Create IEmailService interface**

Create `src/SocialDDD.Application/Interfaces/IEmailService.cs`:

```csharp
namespace SocialDDD.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create RegisterPendingRequest DTO**

Create `src/SocialDDD.Application/Users/DTOs/RegisterPendingRequest.cs`:

```csharp
namespace SocialDDD.Application.Users.DTOs;

public sealed record RegisterPendingRequest(
    string Username,
    string Email,
    string Password,
    string Handle,
    string DisplayName);
```

- [ ] **Step 3: Create VerifyRegistrationRequest DTO**

Create `src/SocialDDD.Application/Users/DTOs/VerifyRegistrationRequest.cs`:

```csharp
namespace SocialDDD.Application.Users.DTOs;

public sealed record VerifyRegistrationRequest(string Email, string Code);
```

- [ ] **Step 4: Build Application layer**

```
dotnet build src/SocialDDD.Application
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Application/Interfaces/IEmailService.cs src/SocialDDD.Application/Users/DTOs/RegisterPendingRequest.cs src/SocialDDD.Application/Users/DTOs/VerifyRegistrationRequest.cs
git commit -m "feat: add IEmailService interface and registration DTOs"
```

---

## Task 5: Application — RegisterPendingUserCommand handler

**Files:**
- Create: `src/SocialDDD.Application/Users/Commands/RegisterPendingUserCommand.cs`

- [ ] **Step 1: Create the handler**

Create `src/SocialDDD.Application/Users/Commands/RegisterPendingUserCommand.cs`:

```csharp
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class RegisterPendingUserCommand(
    IUserRepository userRepository,
    IVerificationCodeRepository codeRepository,
    IPasswordHasher passwordHasher,
    IEmailService emailService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task ExecuteAsync(RegisterPendingRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var username = new Username(request.Username);
        var handle = new Handle(request.Handle);
        var displayName = new DisplayName(request.DisplayName);

        if (await userRepository.ExistsByEmailAsync(email, ct))
            throw new DomainException("Email is already registered.");

        if (await userRepository.ExistsByUsernameAsync(username, ct))
            throw new DomainException("Username is already taken.");

        if (await userRepository.HandleExistsAsync(handle, ct))
            throw new DomainException("Handle is already taken.");

        var hash = passwordHasher.Hash(request.Password);
        var user = User.Register(username, email, new PasswordHash(hash), handle, displayName);

        await userRepository.AddAsync(user, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        var code = new VerificationCode(
            GenerateCode(),
            DateTimeOffset.UtcNow.AddMinutes(15));

        await codeRepository.SaveAsync(user.Id, code, ct);
        await emailService.SendVerificationEmailAsync(email.Value, code.Code, ct);
    }

    private static string GenerateCode()
    {
        var value = Random.Shared.Next(0, 1_000_000);
        return value.ToString("D6");
    }
}
```

- [ ] **Step 2: Build Application layer**

```
dotnet build src/SocialDDD.Application
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/SocialDDD.Application/Users/Commands/RegisterPendingUserCommand.cs
git commit -m "feat: add RegisterPendingUserCommand handler"
```

---

## Task 6: Application — VerifyRegistrationCommand handler

**Files:**
- Create: `src/SocialDDD.Application/Users/Commands/VerifyRegistrationCommand.cs`

- [ ] **Step 1: Write failing handler unit tests**

Create `tests/SocialDDD.Domain.Tests/VerifyRegistrationHandlerTests.cs`:

```csharp
using FluentAssertions;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
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
        public Task DispatchAsync(IReadOnlyList<SocialDDD.Domain.Primitives.IDomainEvent> events, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Add project reference to Application in test project**

Modify `tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj` to add Application reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\SocialDDD.Domain\SocialDDD.Domain.csproj" />
  <ProjectReference Include="..\..\src\SocialDDD.Application\SocialDDD.Application.csproj" />
</ItemGroup>
```

- [ ] **Step 3: Run tests to verify they fail (compile error)**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "VerifyRegistration" -v minimal
```
Expected: compile error (VerifyRegistrationCommand not defined).

- [ ] **Step 4: Create VerifyRegistrationCommand handler**

Create `src/SocialDDD.Application/Users/Commands/VerifyRegistrationCommand.cs`:

```csharp
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Commands;

public sealed class VerifyRegistrationCommand(
    IUserRepository userRepository,
    IVerificationCodeRepository codeRepository,
    ITokenService tokenService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<TokenResponse> ExecuteAsync(VerifyRegistrationRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);

        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainException("No pending registration found for that email.");

        var stored = await codeRepository.FindByUserIdAsync(user.Id, ct)
            ?? throw new DomainException("No verification code found. Please register again.");

        if (stored.IsExpired(DateTimeOffset.UtcNow))
            throw new DomainException("Verification code has expired. Please register again.");

        if (stored.Code != request.Code)
            throw new DomainException("Invalid verification code.");

        user.Activate();

        await userRepository.UpdateAsync(user, ct);
        await codeRepository.DeleteAsync(user.Id, ct);
        await eventDispatcher.DispatchAsync(user.PopDomainEvents(), ct);

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }
}
```

- [ ] **Step 5: Run handler tests**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "VerifyRegistration" -v minimal
```
Expected: 4 tests PASS.

- [ ] **Step 6: Run all tests**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```
Expected: all tests PASS.

- [ ] **Step 7: Commit**

```
git add src/SocialDDD.Application/Users/Commands/VerifyRegistrationCommand.cs tests/SocialDDD.Domain.Tests/VerifyRegistrationHandlerTests.cs tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj
git commit -m "feat: add VerifyRegistrationCommand handler with unit tests"
```

---

## Task 7: Application — Update LoginAsync to reject Pending users

**Files:**
- Modify: `src/SocialDDD.Application/Users/UserService.cs`

- [ ] **Step 1: Update LoginAsync**

In `src/SocialDDD.Application/Users/UserService.cs`, replace the `LoginAsync` method:

```csharp
public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
{
    var email = new Email(request.Email);
    var user = await userRepository.GetByEmailAsync(email, ct)
        ?? throw new DomainException("Invalid credentials.");

    if (!passwordHasher.Verify(request.Password, user.PasswordHash.Value))
        throw new DomainException("Invalid credentials.");

    if (user.Status == UserStatus.Pending)
        throw new DomainException("Account is not yet verified. Please check your email.");

    return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
}
```

Also add the using at the top of `UserService.cs`:
```csharp
using SocialDDD.Domain.Users;
```
(Already present.)

- [ ] **Step 2: Update RegisterAsync to use RegisterImmediate**

In `src/SocialDDD.Application/Users/UserService.cs`, change the `Register` call inside `RegisterAsync`:

Replace:
```csharp
var user = User.Register(username, email, new PasswordHash(hash), handle, displayName);
```

With:
```csharp
var user = User.RegisterImmediate(username, email, new PasswordHash(hash), handle, displayName);
```

- [ ] **Step 3: Build and test**

```
dotnet build src/SocialDDD.Application && dotnet test tests/SocialDDD.Domain.Tests -v minimal
```
Expected: build success, all tests PASS.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Application/Users/UserService.cs
git commit -m "feat: reject Pending users in LoginAsync, use RegisterImmediate for dev path"
```

---

## Task 8: Infrastructure — Email services

**Files:**
- Create: `src/SocialDDD.Infrastructure/Email/ConsoleEmailService.cs`
- Create: `src/SocialDDD.Infrastructure/Email/AzureCommunicationEmailService.cs`

- [ ] **Step 1: Create ConsoleEmailService**

Create `src/SocialDDD.Infrastructure/Email/ConsoleEmailService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Email;

internal sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
    {
        logger.LogInformation("Verification email to {Email}: code = {Code}", toEmail, code);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Create AzureCommunicationEmailService stub**

Create `src/SocialDDD.Infrastructure/Email/AzureCommunicationEmailService.cs`:

```csharp
using SocialDDD.Application.Interfaces;

namespace SocialDDD.Infrastructure.Email;

internal sealed class AzureCommunicationEmailService : IEmailService
{
    public Task SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureCommunicationEmailService is not yet implemented.");
    }
}
```

- [ ] **Step 3: Build Infrastructure**

```
dotnet build src/SocialDDD.Infrastructure
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Infrastructure/Email/ConsoleEmailService.cs src/SocialDDD.Infrastructure/Email/AzureCommunicationEmailService.cs
git commit -m "feat: add ConsoleEmailService and AzureCommunicationEmailService stub"
```

---

## Task 9: Infrastructure — VerificationCode repositories

**Files:**
- Create: `src/SocialDDD.Infrastructure/Persistence/VerificationCodes/InMemoryVerificationCodeRepository.cs`
- Create: `src/SocialDDD.Infrastructure/Persistence/VerificationCodes/MongoDbVerificationCodeRepository.cs`

The MongoDb repository stores a document with `userId` (string), `code` (string), and `expiresAt` (DateTimeOffset as BsonDateTime). A TTL index on `expiresAt` auto-deletes expired documents.

- [ ] **Step 1: Create InMemoryVerificationCodeRepository**

Create `src/SocialDDD.Infrastructure/Persistence/VerificationCodes/InMemoryVerificationCodeRepository.cs`:

```csharp
using System.Collections.Concurrent;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.VerificationCodes;

internal sealed class InMemoryVerificationCodeRepository : IVerificationCodeRepository
{
    private readonly ConcurrentDictionary<string, VerificationCode> _store = new();

    public Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default)
    {
        _store[userId.Value.ToString()] = code;
        return Task.CompletedTask;
    }

    public Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        _store.TryGetValue(userId.Value.ToString(), out var code);
        return Task.FromResult(code);
    }

    public Task DeleteAsync(UserId userId, CancellationToken ct = default)
    {
        _store.TryRemove(userId.Value.ToString(), out _);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Create MongoDbVerificationCodeRepository**

Create `src/SocialDDD.Infrastructure/Persistence/VerificationCodes/MongoDbVerificationCodeRepository.cs`:

```csharp
using MongoDB.Driver;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Persistence;

namespace SocialDDD.Infrastructure.Persistence.VerificationCodes;

internal sealed class MongoDbVerificationCodeRepository(MongoDbContext context) : IVerificationCodeRepository
{
    public Task SaveAsync(UserId userId, VerificationCode code, CancellationToken ct = default)
    {
        var doc = new VerificationCodeDocument(userId.Value.ToString(), code.Code, code.ExpiresAt.UtcDateTime);
        return context.VerificationCodes.ReplaceOneAsync(
            d => d.UserId == doc.UserId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<VerificationCode?> FindByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var key = userId.Value.ToString();
        var doc = await context.VerificationCodes
            .Find(d => d.UserId == key)
            .FirstOrDefaultAsync(ct);

        if (doc is null) return null;
        return new VerificationCode(doc.Code, new DateTimeOffset(doc.ExpiresAt, TimeSpan.Zero));
    }

    public Task DeleteAsync(UserId userId, CancellationToken ct = default)
    {
        var key = userId.Value.ToString();
        return context.VerificationCodes.DeleteOneAsync(d => d.UserId == key, ct);
    }
}

internal sealed class VerificationCodeDocument(string userId, string code, DateTime expiresAt)
{
    public string UserId { get; init; } = userId;
    public string Code { get; init; } = code;
    public DateTime ExpiresAt { get; init; } = expiresAt;
}
```

- [ ] **Step 3: Build Infrastructure**

```
dotnet build src/SocialDDD.Infrastructure
```
Expected: errors because `MongoDbContext.VerificationCodes` does not exist yet — that is expected at this step.

- [ ] **Step 4: Commit (partial)**

```
git add src/SocialDDD.Infrastructure/Persistence/VerificationCodes/InMemoryVerificationCodeRepository.cs src/SocialDDD.Infrastructure/Persistence/VerificationCodes/MongoDbVerificationCodeRepository.cs
git commit -m "feat: add InMemory and MongoDb VerificationCode repositories"
```

---

## Task 10: Infrastructure — Update MongoDbContext for VerificationCodes collection

**Files:**
- Modify: `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs`

- [ ] **Step 1: Add VerificationCodes collection and TTL index**

Replace `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs` entirely:

```csharp
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Persistence.Mapping;
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
    public IMongoCollection<VerificationCodeDocument> VerificationCodes =>
        _database.GetCollection<VerificationCodeDocument>("verification_codes");

    private void EnsureIndexes()
    {
        var handleIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending("handle"),
            new CreateIndexOptions { Unique = true, Background = true, Name = "handle_unique" });

        Users.Indexes.CreateOne(handleIndex);

        var ttlIndex = new CreateIndexModel<VerificationCodeDocument>(
            Builders<VerificationCodeDocument>.IndexKeys.Ascending(d => d.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_ttl" });

        VerificationCodes.Indexes.CreateOne(ttlIndex);
    }
}
```

- [ ] **Step 2: Build Infrastructure**

```
dotnet build src/SocialDDD.Infrastructure
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs
git commit -m "feat: add VerificationCodes collection with TTL index to MongoDbContext"
```

---

## Task 11: Infrastructure — Update DependencyInjection.cs

**Files:**
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Register new services**

Replace `src/SocialDDD.Infrastructure/DependencyInjection.cs` entirely:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;
using SocialDDD.Infrastructure.Auth;
using SocialDDD.Infrastructure.Email;
using SocialDDD.Infrastructure.Events;
using SocialDDD.Infrastructure.Persistence;
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
        services.AddSingleton<IVerificationCodeRepository, InMemoryVerificationCodeRepository>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IEmailService, ConsoleEmailService>();

        services.AddScoped<RegisterPendingUserCommand>();
        services.AddScoped<VerifyRegistrationCommand>();

        return services;
    }
}
```

- [ ] **Step 2: Build entire solution**

```
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run all tests**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```
Expected: all tests PASS.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Infrastructure/DependencyInjection.cs
git commit -m "feat: register verification code repository, email service, and command handlers"
```

---

## Task 12: API — RegistrationsController

**Files:**
- Create: `src/SocialDDD.Api/Controllers/RegistrationsController.cs`

- [ ] **Step 1: Create the controller**

Create `src/SocialDDD.Api/Controllers/RegistrationsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users.Commands;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/registrations")]
public sealed class RegistrationsController(
    RegisterPendingUserCommand registerCommand,
    VerifyRegistrationCommand verifyCommand) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterPendingRequest request, CancellationToken ct)
    {
        try
        {
            await registerCommand.ExecuteAsync(request, ct);
            return Accepted(new { message = "Registration started. Check your email for a verification code." });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRegistrationRequest request, CancellationToken ct)
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

- [ ] **Step 2: Build API**

```
dotnet build src/SocialDDD.Api
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/SocialDDD.Api/Controllers/RegistrationsController.cs
git commit -m "feat: add RegistrationsController with POST /api/registrations and POST /api/registrations/verify"
```

---

## Task 13: Final build, test, and commit

- [ ] **Step 1: Build entire solution**

```
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run all tests**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```
Expected: all tests PASS.

- [ ] **Step 3: Final commit**

```
git commit --allow-empty -m "feat: add email verification registration flow (prompt 02)"
```

Or if there are uncommitted changes after step 1 and 2:
```
git add -A
git commit -m "feat: add email verification registration flow (prompt 02)"
```

---

## Self-Review

### Spec coverage

| Spec requirement | Task |
|---|---|
| `UserStatus` enum | Task 1 |
| `Status` on `User` | Task 3 |
| `User.Register` creates Pending | Task 3 |
| `User.Activate()` + `UserActivated` event | Task 2, 3 |
| `VerificationCode` value object with `IsExpired` | Task 1 |
| `IVerificationCodeRepository` interface | Task 2 |
| `IEmailService` interface | Task 4 |
| `RegisterPendingUserCommand` | Task 5 |
| `VerifyRegistrationCommand` | Task 6 |
| `LoginAsync` rejects Pending | Task 7 |
| `POST /api/accounts` uses `RegisterImmediate` (immediate/Active) | Task 7 |
| `InMemoryVerificationCodeRepository` | Task 9 |
| `MongoDbVerificationCodeRepository` | Task 9, 10 |
| `ConsoleEmailService` | Task 8 |
| `AzureCommunicationEmailService` stub | Task 8 |
| `BsonMappings.cs` update | Not needed — `UserStatus` is an enum, MongoDB C# driver serializes enums as integers by default; no custom serializer required. The `VerificationCodeDocument` is a plain POCO, not a domain type requiring a custom serializer. |
| `DependencyInjection.cs` update | Task 11 |
| `POST /api/registrations` | Task 12 |
| `POST /api/registrations/verify` | Task 12 |
| Unit tests: VerificationCode expiry | Task 1 |
| Unit tests: User.Activate() raises event | Task 3 |
| Unit tests: verify handler (expired, wrong, correct) | Task 6 |

### Placeholder scan

No TBD, TODO, or vague steps found.

### Type consistency

- `VerificationCode(string Code, DateTimeOffset ExpiresAt)` — used consistently in Tasks 1, 6, 9.
- `IVerificationCodeRepository.SaveAsync/FindByUserIdAsync/DeleteAsync` — signatures match across Tasks 2, 5, 6, 9.
- `User.RegisterImmediate(...)` — same signature as `User.Register(...)` — used in Task 3, Task 7.
- `RegisterPendingUserCommand.ExecuteAsync(RegisterPendingRequest)` — used in Task 5, Task 12.
- `VerifyRegistrationCommand.ExecuteAsync(VerifyRegistrationRequest)` — used in Task 6, Task 12.
- `TokenResponse` returned by `VerifyRegistrationCommand` — matches existing record in `SocialDDD.Application.Users.DTOs`.
- `FakeUserRepository` in tests implements all 10 `IUserRepository` methods — matches interface definition.

### Edge cases confirmed

- `InMemoryVerificationCodeRepository` is registered as `Singleton` (correct — it's an in-memory dictionary that must outlive request scope).
- `ConsoleEmailService` is registered as `Scoped` (correct — it holds no state, logger is injected per-scope).
- `RegisterPendingUserCommand` and `VerifyRegistrationCommand` are `Scoped` (correct — they depend on scoped repositories).
