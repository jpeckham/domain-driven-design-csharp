# User Handle + DisplayName Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Handle` and `DisplayName` value objects to the `User` aggregate, with uniqueness enforcement, MongoDB persistence, and four new/updated API endpoints.

**Architecture:** Strict DDD — value objects validate themselves and throw `DomainException`; the domain layer owns the contracts; infrastructure implements them; the application layer orchestrates. No CQRS command objects — this codebase uses service methods with DTOs directly.

**Tech Stack:** C# 12, .NET 8, MongoDB driver, xUnit, FluentAssertions

---

## File Map

**New files:**
- `src/SocialDDD.Domain/Users/Handle.cs`
- `src/SocialDDD.Domain/Users/DisplayName.cs`
- `src/SocialDDD.Application/Users/DTOs/UpdateDisplayNameRequest.cs`

**Modified files:**
- `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs` — Handle and DisplayName tests
- `tests/SocialDDD.Domain.Tests/UserTests.cs` — User.Register with handle tests
- `src/SocialDDD.Domain/Users/Events/UserRegistered.cs` — add Handle, DisplayName to payload
- `src/SocialDDD.Domain/Users/User.cs` — add properties, update Register, add UpdateDisplayName
- `src/SocialDDD.Domain/Users/IUserRepository.cs` — add FindByHandleAsync, HandleExistsAsync
- `src/SocialDDD.Application/Users/DTOs/RegisterRequest.cs` — add Handle, DisplayName
- `src/SocialDDD.Application/Users/DTOs/UserDto.cs` — add Handle, DisplayName
- `src/SocialDDD.Application/Users/UserService.cs` — update RegisterAsync, add GetByHandleAsync, UpdateDisplayNameAsync
- `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs` — serializers for Handle, DisplayName; class map update; unique index
- `src/SocialDDD.Infrastructure/Persistence/Users/UserRepository.cs` — implement new methods
- `src/SocialDDD.Api/Controllers/UsersController.cs` — add by-handle and update-display-name endpoints

---

## Task 1: Handle Value Object

**Files:**
- Create: `src/SocialDDD.Domain/Users/Handle.cs`
- Modify: `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs`

- [ ] **Step 1: Write failing tests for Handle**

Add to `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs` (inside the existing file, after existing tests):

```csharp
// Handle tests
[Theory]
[InlineData("alice")]
[InlineData("Alice")]          // normalized to lowercase
[InlineData("alice_123")]
[InlineData("a")]              // min length
[InlineData("a123456789012345678901234567890")] // 31 chars - boundary skip, 30 is max
public void Handle_ValidInput_CreatesHandle(string input)
{
    var handle = new Handle(input);
    handle.Value.Should().Be(input.ToLowerInvariant());
    handle.Display.Should().Be("@" + input.ToLowerInvariant());
}

[Theory]
[InlineData("")]               // empty
[InlineData("   ")]            // whitespace
[InlineData("alice smith")]    // space
[InlineData("@alice")]         // @ prefix not allowed
[InlineData("alice!")]         // invalid char
[InlineData("alice-bob")]      // hyphen not allowed
[InlineData("1234567890123456789012345678901")] // 31 chars — too long
public void Handle_InvalidInput_ThrowsDomainException(string input)
{
    var act = () => new Handle(input);
    act.Should().Throw<DomainException>();
}

[Fact]
public void Handle_NormalizesToLowercase()
{
    var handle = new Handle("ALICE");
    handle.Value.Should().Be("alice");
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "Handle" -v minimal
```

Expected: compile error — `Handle` type does not exist.

- [ ] **Step 3: Implement Handle value object**

Create `src/SocialDDD.Domain/Users/Handle.cs`:

```csharp
using SocialDDD.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace SocialDDD.Domain.Users;

public sealed record Handle
{
    private static readonly Regex ValidPattern = new(@"^[a-z0-9_]{1,30}$", RegexOptions.Compiled);

    public string Value { get; }
    public string Display => "@" + Value;

    public Handle(string value)
    {
        var normalized = value?.ToLowerInvariant() ?? string.Empty;
        if (!ValidPattern.IsMatch(normalized))
            throw new DomainException("Handle must be 1–30 characters: letters, digits, and underscores only.");
        Value = normalized;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "Handle" -v minimal
```

Expected: all Handle tests PASS.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Domain/Users/Handle.cs tests/SocialDDD.Domain.Tests/ValueObjectTests.cs
git commit -m "feat: add Handle value object with validation and case normalization"
```

---

## Task 2: DisplayName Value Object

**Files:**
- Create: `src/SocialDDD.Domain/Users/DisplayName.cs`
- Modify: `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs`

- [ ] **Step 1: Write failing tests for DisplayName**

Add to `tests/SocialDDD.Domain.Tests/ValueObjectTests.cs`:

```csharp
// DisplayName tests
[Theory]
[InlineData("Alice Smith")]
[InlineData("  Alice  ")]    // trimmed
[InlineData("A")]            // min length after trim
public void DisplayName_ValidInput_CreatesDisplayName(string input)
{
    var dn = new DisplayName(input);
    dn.Value.Should().Be(input.Trim());
}

[Theory]
[InlineData("")]                   // empty
[InlineData("   ")]                // whitespace only
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "DisplayName" -v minimal
```

Expected: compile error — `DisplayName` type does not exist.

- [ ] **Step 3: Implement DisplayName value object**

Create `src/SocialDDD.Domain/Users/DisplayName.cs`:

```csharp
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Domain.Users;

public sealed record DisplayName
{
    public string Value { get; }

    public DisplayName(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > 50)
            throw new DomainException("DisplayName must be 1–50 characters.");
        Value = trimmed;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "DisplayName" -v minimal
```

Expected: all DisplayName tests PASS.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Domain/Users/DisplayName.cs tests/SocialDDD.Domain.Tests/ValueObjectTests.cs
git commit -m "feat: add DisplayName value object with trimming and length validation"
```

---

## Task 3: Update Domain — UserRegistered Event + User Aggregate

**Files:**
- Modify: `src/SocialDDD.Domain/Users/Events/UserRegistered.cs`
- Modify: `src/SocialDDD.Domain/Users/User.cs`
- Modify: `tests/SocialDDD.Domain.Tests/UserTests.cs`

- [ ] **Step 1: Write failing tests for User.Register with handle**

Add to `tests/SocialDDD.Domain.Tests/UserTests.cs`:

```csharp
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

    user.PopDomainEvents(); // clear registration event

    var newName = new DisplayName("Alice Smith");
    user.UpdateDisplayName(newName);

    user.DisplayName.Should().Be(newName);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/SocialDDD.Domain.Tests --filter "Handle|DisplayName|UpdateDisplayName" -v minimal
```

Expected: compile errors — `Handle` and `DisplayName` not on `User`, wrong `Register` signature.

- [ ] **Step 3: Update UserRegistered event**

Replace `src/SocialDDD.Domain/Users/Events/UserRegistered.cs`:

```csharp
using SocialDDD.Domain.Primitives;

namespace SocialDDD.Domain.Users.Events;

public sealed record UserRegistered(UserId UserId, Handle Handle, DisplayName DisplayName) : IDomainEvent;
```

- [ ] **Step 4: Update User aggregate**

Replace `src/SocialDDD.Domain/Users/User.cs`:

```csharp
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
            RegisteredAt = DateTime.UtcNow
        };
        user.RaiseDomainEvent(new UserRegistered(user.Id, handle, displayName));
        return user;
    }

    public void UpdateDisplayName(DisplayName newName)
    {
        DisplayName = newName;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```

Expected: all tests PASS (the existing `Register_ValidArgs` test now has a compile error — fix it in the next step).

- [ ] **Step 6: Fix existing UserTests to compile with new signature**

In `tests/SocialDDD.Domain.Tests/UserTests.cs`, update the existing two tests to pass `Handle` and `DisplayName`:

```csharp
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
    user.Handle.Value.Should().Be("alice");
    user.DisplayName.Value.Should().Be("Alice Smith");
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
        new DisplayName("Alice"));

    act.Should().Throw<DomainException>();
}
```

- [ ] **Step 7: Run all domain tests**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```

Expected: all tests PASS.

- [ ] **Step 8: Commit**

```
git add src/SocialDDD.Domain/Users/Events/UserRegistered.cs src/SocialDDD.Domain/Users/User.cs tests/SocialDDD.Domain.Tests/UserTests.cs
git commit -m "feat: add Handle and DisplayName to User aggregate and UserRegistered event"
```

---

## Task 4: Update IUserRepository

**Files:**
- Modify: `src/SocialDDD.Domain/Users/IUserRepository.cs`

- [ ] **Step 1: Add handle lookup methods to the interface**

Replace `src/SocialDDD.Domain/Users/IUserRepository.cs`:

```csharp
namespace SocialDDD.Domain.Users;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct = default);
    Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default);
    Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default);
    Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default);
    Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
}
```

- [ ] **Step 2: Verify solution still compiles (UserRepository will have errors — that's expected)**

```
dotnet build src/SocialDDD.Infrastructure
```

Expected: compile errors in `UserRepository.cs` — `FindByHandleAsync` and `HandleExistsAsync` not implemented. This is expected; implemented in Task 7.

- [ ] **Step 3: Commit**

```
git add src/SocialDDD.Domain/Users/IUserRepository.cs
git commit -m "feat: add FindByHandleAsync, HandleExistsAsync, UpdateAsync to IUserRepository"
```

---

## Task 5: Update Application DTOs

**Files:**
- Modify: `src/SocialDDD.Application/Users/DTOs/RegisterRequest.cs`
- Modify: `src/SocialDDD.Application/Users/DTOs/UserDto.cs`
- Create: `src/SocialDDD.Application/Users/DTOs/UpdateDisplayNameRequest.cs`

- [ ] **Step 1: Update RegisterRequest**

Replace `src/SocialDDD.Application/Users/DTOs/RegisterRequest.cs`:

```csharp
namespace SocialDDD.Application.Users.DTOs;

public sealed record RegisterRequest(string Username, string Email, string Password, string Handle, string DisplayName);
```

- [ ] **Step 2: Update UserDto**

Replace `src/SocialDDD.Application/Users/DTOs/UserDto.cs`:

```csharp
namespace SocialDDD.Application.Users.DTOs;

public sealed record UserDto(Guid UserId, string Username, string Email, string Handle, string DisplayName, DateTime RegisteredAt);
```

- [ ] **Step 3: Create UpdateDisplayNameRequest**

Create `src/SocialDDD.Application/Users/DTOs/UpdateDisplayNameRequest.cs`:

```csharp
namespace SocialDDD.Application.Users.DTOs;

public sealed record UpdateDisplayNameRequest(string DisplayName);
```

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Application/Users/DTOs/RegisterRequest.cs src/SocialDDD.Application/Users/DTOs/UserDto.cs src/SocialDDD.Application/Users/DTOs/UpdateDisplayNameRequest.cs
git commit -m "feat: update application DTOs to include Handle and DisplayName fields"
```

---

## Task 6: Update UserService

**Files:**
- Modify: `src/SocialDDD.Application/Users/UserService.cs`

- [ ] **Step 1: Update UserService**

Replace `src/SocialDDD.Application/Users/UserService.cs`:

```csharp
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDomainEventDispatcher eventDispatcher)
{
    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
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

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = new Email(request.Email);
        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new DomainException("Invalid credentials.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash.Value))
            throw new DomainException("Invalid credentials.");

        return new TokenResponse(tokenService.GenerateToken(user), user.Id.Value, user.Username.Value);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(id), ct)
            ?? throw new DomainException($"User {id} not found.");

        return ToDto(user);
    }

    public async Task<UserDto> GetByHandleAsync(string rawHandle, CancellationToken ct = default)
    {
        var handle = new Handle(rawHandle);
        var user = await userRepository.FindByHandleAsync(handle, ct)
            ?? throw new DomainException($"User with handle @{handle.Value} not found.");

        return ToDto(user);
    }

    public async Task UpdateDisplayNameAsync(Guid id, UpdateDisplayNameRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(id), ct)
            ?? throw new DomainException($"User {id} not found.");

        user.UpdateDisplayName(new DisplayName(request.DisplayName));
        await userRepository.UpdateAsync(user, ct);
    }

    private static UserDto ToDto(User user) =>
        new(user.Id.Value, user.Username.Value, user.Email.Value, user.Handle.Display, user.DisplayName.Value, user.RegisteredAt);
}
```

- [ ] **Step 2: Verify Application layer compiles**

```
dotnet build src/SocialDDD.Application
```

Expected: PASS (Application has no dependency on Infrastructure).

- [ ] **Step 3: Commit**

```
git add src/SocialDDD.Application/Users/UserService.cs
git commit -m "feat: update UserService with handle uniqueness check and new service methods"
```

---

## Task 7: Update Infrastructure — BSON Mappings + UserRepository

**Files:**
- Modify: `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs`
- Modify: `src/SocialDDD.Infrastructure/Persistence/Users/UserRepository.cs`

- [ ] **Step 1: Update BsonMappings**

Replace `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs` with the full updated version (add Handle and DisplayName serializers, update the User class map to set element names, and create the unique index on handle):

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Mapping;

internal static class BsonMappings
{
    private static int _registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0) return;

        BsonSerializer.RegisterSerializer(new UserIdSerializer());
        BsonSerializer.RegisterSerializer(new PostIdSerializer());
        BsonSerializer.RegisterSerializer(new UsernameSerializer());
        BsonSerializer.RegisterSerializer(new EmailSerializer());
        BsonSerializer.RegisterSerializer(new PasswordHashSerializer());
        BsonSerializer.RegisterSerializer(new PostContentSerializer());
        BsonSerializer.RegisterSerializer(new HandleSerializer());
        BsonSerializer.RegisterSerializer(new DisplayNameSerializer());

        BsonClassMap.RegisterClassMap<User>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(u => u.Handle).SetElementName("handle");
            cm.MapMember(u => u.DisplayName).SetElementName("displayName");
        });

        BsonClassMap.RegisterClassMap<Post>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });
    }

    // Existing serializers (unchanged) -------------------------

    internal sealed class UserIdSerializer : SerializerBase<UserId>
    {
        public override UserId Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => UserId.From(Guid.Parse(ctx.Reader.ReadString()));

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, UserId value)
            => ctx.Writer.WriteString(value.Value.ToString());
    }

    internal sealed class PostIdSerializer : SerializerBase<PostId>
    {
        public override PostId Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => PostId.From(Guid.Parse(ctx.Reader.ReadString()));

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, PostId value)
            => ctx.Writer.WriteString(value.Value.ToString());
    }

    internal sealed class UsernameSerializer : SerializerBase<Username>
    {
        public override Username Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => new(ctx.Reader.ReadString());

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, Username value)
            => ctx.Writer.WriteString(value.Value);
    }

    internal sealed class EmailSerializer : SerializerBase<Email>
    {
        public override Email Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => new(ctx.Reader.ReadString());

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, Email value)
            => ctx.Writer.WriteString(value.Value);
    }

    internal sealed class PasswordHashSerializer : SerializerBase<PasswordHash>
    {
        public override PasswordHash Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => new(ctx.Reader.ReadString());

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, PasswordHash value)
            => ctx.Writer.WriteString(value.Value);
    }

    internal sealed class PostContentSerializer : SerializerBase<PostContent>
    {
        public override PostContent Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => new(ctx.Reader.ReadString());

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, PostContent value)
            => ctx.Writer.WriteString(value.Value);
    }

    // New serializers -------------------------------------------

    internal sealed class HandleSerializer : SerializerBase<Handle>
    {
        public override Handle Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => new(ctx.Reader.ReadString());

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, Handle value)
            => ctx.Writer.WriteString(value.Value);
    }

    internal sealed class DisplayNameSerializer : SerializerBase<DisplayName>
    {
        public override DisplayName Deserialize(BsonDeserializationContext ctx, BsonDeserializationArgs args)
            => new(ctx.Reader.ReadString());

        public override void Serialize(BsonSerializationContext ctx, BsonSerializationArgs args, DisplayName value)
            => ctx.Writer.WriteString(value.Value);
    }
}
```

- [ ] **Step 2: Add unique index creation to MongoDbContext**

In `src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs`, after `BsonMappings.Register()`, add the index creation:

```csharp
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

    private void EnsureIndexes()
    {
        var handleIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending("handle"),
            new CreateIndexOptions { Unique = true, Background = true, Name = "handle_unique" });

        Users.Indexes.CreateOne(handleIndex);
    }
}
```

- [ ] **Step 3: Implement FindByHandleAsync and HandleExistsAsync in UserRepository**

In `src/SocialDDD.Infrastructure/Persistence/Users/UserRepository.cs`, add the two new methods and `UpdateAsync`. The full file:

```csharp
using MongoDB.Driver;
using SocialDDD.Domain.Users;

namespace SocialDDD.Infrastructure.Persistence.Users;

internal sealed class UserRepository(MongoDbContext context) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken ct = default) =>
        context.Users.InsertOneAsync(user, cancellationToken: ct);

    public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ct);

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);

    public async Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Username == username).FirstOrDefaultAsync(ct);

    public async Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Handle == handle).FirstOrDefaultAsync(ct);

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Email == email).AnyAsync(ct);

    public async Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Username == username).AnyAsync(ct);

    public async Task<bool> ExistsByIdAsync(UserId id, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Id == id).AnyAsync(ct);

    public async Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct = default) =>
        await context.Users.Find(u => u.Handle == handle).AnyAsync(ct);

    public Task UpdateAsync(User user, CancellationToken ct = default) =>
        context.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: ct);
}
```

- [ ] **Step 4: Verify Infrastructure compiles**

```
dotnet build src/SocialDDD.Infrastructure
```

Expected: PASS.

- [ ] **Step 5: Commit**

```
git add src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs src/SocialDDD.Infrastructure/Persistence/MongoDbContext.cs src/SocialDDD.Infrastructure/Persistence/Users/UserRepository.cs
git commit -m "feat: add Handle/DisplayName BSON serializers, unique handle index, and repository methods"
```

---

## Task 8: Update API Controller

**Files:**
- Modify: `src/SocialDDD.Api/Controllers/UsersController.cs`

- [ ] **Step 1: Update UsersController**

Replace `src/SocialDDD.Api/Controllers/UsersController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Users;
using SocialDDD.Application.Users.DTOs;
using SocialDDD.Domain.Exceptions;

namespace SocialDDD.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(UserService userService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var response = await userService.RegisterAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = response.UserId }, response);
        }
        catch (DomainException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var response = await userService.LoginAsync(request, ct);
            return Ok(response);
        }
        catch (DomainException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var user = await userService.GetByIdAsync(id, ct);
            return Ok(user);
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("by-handle/{handle}")]
    public async Task<IActionResult> GetByHandle(string handle, CancellationToken ct)
    {
        try
        {
            var user = await userService.GetByHandleAsync(handle, ct);
            return Ok(user);
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("me/display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token." });

        try
        {
            await userService.UpdateDisplayNameAsync(userId, request, ct);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
```

- [ ] **Step 2: Verify full solution builds**

```
dotnet build
```

Expected: PASS with no errors.

- [ ] **Step 3: Run all tests**

```
dotnet test tests/SocialDDD.Domain.Tests -v minimal
```

Expected: all tests PASS.

- [ ] **Step 4: Commit**

```
git add src/SocialDDD.Api/Controllers/UsersController.cs
git commit -m "feat: add by-handle lookup and update-display-name endpoints to UsersController"
```

---

## Self-Review Against Spec

| Requirement | Task |
|---|---|
| `Handle` value object (letters/digits/underscores, 1–30, case-insensitive, `Value`/`Display`) | Task 1 |
| `DisplayName` value object (1–50, trimmed) | Task 2 |
| `User.Handle`, `User.DisplayName`, updated `Register` factory | Task 3 |
| `UserRegistered` event includes handle/displayName | Task 3 |
| `User.UpdateDisplayName(DisplayName)` | Task 3 |
| `IUserRepository.FindByHandleAsync` + `HandleExistsAsync` | Task 4 |
| `RegisterRequest` includes Handle + DisplayName | Task 5 |
| Handle uniqueness check in `RegisterAsync` | Task 6 |
| `UserDto` exposes Handle + DisplayName | Task 5 |
| `UpdateDisplayNameAsync` service method | Task 6 |
| BSON serializers for Handle + DisplayName | Task 7 |
| `UserRepository` implements new methods | Task 7 |
| Unique MongoDB index on `handle` | Task 7 |
| `POST /api/users/register` accepts handle + displayName | Task 8 |
| `GET /api/users/{id}` returns handle + displayName | Task 5+8 (UserDto) |
| `GET /api/users/by-handle/{handle}` endpoint | Task 8 |
| `PUT /api/users/me/display-name` endpoint (authenticated) | Task 8 |
| Unit tests: Handle valid/invalid/normalization | Task 1 |
| Unit tests: DisplayName valid/invalid/trimming | Task 2 |
| Unit tests: `User.Register` produces correct handle in event | Task 3 |
