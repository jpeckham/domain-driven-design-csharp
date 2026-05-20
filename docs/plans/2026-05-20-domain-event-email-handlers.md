# Domain Event Email Handlers Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Move registration, password reset, and login challenge email side effects into domain event handlers.

**Architecture:** Add typed domain event handlers and make the dispatcher resolve registered handlers. Commands save required state, dispatch events, and stop calling `IEmailService` directly. Infrastructure handlers perform email work.

**Tech Stack:** .NET 9, xUnit, FluentAssertions, Microsoft.Extensions.DependencyInjection.

---

### Task 1: Handler Contract and Dispatcher

**Files:**
- Create: `src/SocialDDD.Application/Interfaces/IDomainEventHandler.cs`
- Modify: `src/SocialDDD.Infrastructure/Events/DomainEventDispatcher.cs`
- Test: `tests/SocialDDD.Domain.Tests/DomainEventDispatcherTests.cs`

**Step 1:** Write a failing test proving `DomainEventDispatcher` invokes a registered `IDomainEventHandler<TEvent>`.

**Step 2:** Run `dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj --filter DomainEventDispatcherTests`.

**Step 3:** Add `IDomainEventHandler<TEvent>` and implement DI-based dispatch.

**Step 4:** Re-run the dispatcher test.

### Task 2: Registration Verification Email Handler

**Files:**
- Modify: `src/SocialDDD.Domain/Users/Events/UserRegistered.cs`
- Modify: `src/SocialDDD.Domain/Users/User.cs`
- Create: `src/SocialDDD.Infrastructure/Events/Handlers/SendUserRegisteredVerificationEmailHandler.cs`
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`
- Modify: `src/SocialDDD.Application/Users/Commands/RegisterPendingUserCommand.cs`
- Test: `tests/SocialDDD.Domain.Tests/RegisterPendingUserCommandTests.cs`
- Test: `tests/SocialDDD.Domain.Tests/DomainEventEmailHandlerTests.cs`

**Step 1:** Write failing tests proving registration dispatches `UserRegistered` and the handler saves a verification code and sends email.

**Step 2:** Remove direct verification email sending from the command.

**Step 3:** Register the handler in DI.

**Step 4:** Re-run registration and handler tests.

### Task 3: Password Reset Email Handler

**Files:**
- Create: `src/SocialDDD.Domain/Users/Events/PasswordResetRequested.cs`
- Create: `src/SocialDDD.Infrastructure/Events/Handlers/SendPasswordResetRequestedEmailHandler.cs`
- Modify: `src/SocialDDD.Application/Users/Commands/RequestPasswordResetCommand.cs`
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`
- Test: `tests/SocialDDD.Domain.Tests/PasswordResetTests.cs`
- Test: `tests/SocialDDD.Domain.Tests/DomainEventEmailHandlerTests.cs`

**Step 1:** Write failing tests proving password reset requests dispatch an event and the handler sends email.

**Step 2:** Remove direct reset email sending from the command.

**Step 3:** Register the handler in DI.

**Step 4:** Re-run password reset and handler tests.

### Task 4: Login Challenge Email Handler

**Files:**
- Create: `src/SocialDDD.Domain/Users/Events/LoginChallenged.cs`
- Create: `src/SocialDDD.Infrastructure/Events/Handlers/SendLoginChallengedEmailHandler.cs`
- Modify: `src/SocialDDD.Application/Users/Commands/LoginWithDeviceCommand.cs`
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`
- Test: `tests/SocialDDD.Domain.Tests/DeviceMfaTests.cs`
- Test: `tests/SocialDDD.Domain.Tests/DomainEventEmailHandlerTests.cs`

**Step 1:** Write failing tests proving unknown-device login dispatches an event and the handler sends OTP email.

**Step 2:** Remove direct OTP email sending from the command.

**Step 3:** Register the handler in DI.

**Step 4:** Re-run device MFA and handler tests.

### Task 5: Full Verification

Run `dotnet test SocialDDD.sln`.
