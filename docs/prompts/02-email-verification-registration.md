# Prompt 02 — Email Verification Registration Flow

## Context

Currently `POST /api/users/register` creates an active account immediately. The target feature requires a two-step registration:

1. User submits registration details → account is created in **Pending** state, a time-limited verification code is emailed.
2. User submits the code → account becomes **Active**, user can now log in.

The immediate "create account" path (no email verification) should remain available as a separate endpoint for development/testing convenience.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Add a `UserStatus` enum: `Pending`, `Active`.
2. Add `Status` property to the `User` aggregate.
3. Update `User.Register` factory to create users with `Status = Pending`.
4. Add `User.Activate()` method that sets `Status = Active` and raises a `UserActivated` domain event.
5. Add a `VerificationCode` value object:
   - Wraps a `string` code (e.g. 6-digit numeric).
   - Has an `ExpiresAt` (DateTimeOffset).
   - `IsExpired(DateTimeOffset now)` helper.
6. Create `IVerificationCodeRepository` interface:
   - `SaveAsync(UserId userId, VerificationCode code, CancellationToken ct)`
   - `FindByUserIdAsync(UserId userId, CancellationToken ct) → Task<VerificationCode?>`
   - `DeleteAsync(UserId userId, CancellationToken ct)`

### Application Layer (`SocialDDD.Application`)

1. Add `IEmailService` interface with `SendVerificationEmailAsync(string toEmail, string code, CancellationToken ct)`.
2. Add `RegisterPendingUserCommand` and handler:
   - Validate email/handle uniqueness.
   - Create user with `Status = Pending`.
   - Generate a random 6-digit code with 15-minute expiry.
   - Save code via `IVerificationCodeRepository`.
   - Send email via `IEmailService`.
3. Add `VerifyRegistrationCommand` and handler:
   - Load the pending user by email.
   - Load the verification code for that user.
   - Validate the code matches and is not expired.
   - Call `user.Activate()`.
   - Delete the used code.
   - Return an auth token so the user is immediately logged in.
4. Update `LoginAsync` to reject users with `Status = Pending` (with a clear error message).

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `InMemoryVerificationCodeRepository` for development/testing.
2. Implement `MongoDbVerificationCodeRepository` for production (store codes in a `verification_codes` collection with a TTL index on `expiresAt`).
3. Implement `ConsoleEmailService` (logs to console) for development.
4. Implement `AzureCommunicationEmailService` stub (reads connection string from config, sends real emails) — leave the template simple.

### API Layer (`SocialDDD.Api`)

1. Keep `POST /api/accounts` for immediate (no-verification) account creation (development convenience).
2. Add `POST /api/registrations` — creates a pending account and sends the verification email.
3. Add `POST /api/registrations/verify` — accepts `{ email, code }`, activates account, returns bearer token.
4. Wire up `IEmailService` and `IVerificationCodeRepository` in DI (use in-memory by default, swap via config).

### Tests

1. Unit test `VerificationCode` expiry logic.
2. Unit test `User.Activate()` raises `UserActivated` event.
3. Unit test the verify handler: expired code returns error, wrong code returns error, correct code activates user.

## Acceptance Criteria

- `POST /api/registrations` with valid data returns 202 Accepted and sends a code (visible in console during dev).
- `POST /api/registrations/verify` with correct code within 15 min returns a bearer token and `status: active`.
- `POST /api/registrations/verify` with expired or wrong code returns 400 with a clear message.
- `POST /api/sessions` (login) rejects accounts in Pending status.
- `POST /api/accounts` still works for immediate account creation.
