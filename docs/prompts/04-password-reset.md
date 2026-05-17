# Prompt 04 — Password Reset Flow

## Context

Users need a way to recover access when they forget their password. The flow is:

1. User submits their email → a one-time, time-limited reset link (5-minute expiry) is emailed.
2. User clicks the link (which includes the token) → submits a new password.
3. The token is consumed (single-use) and the password is updated.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Create a `PasswordResetToken` value object:
   - `Token` (string — a cryptographically random URL-safe string, e.g. 32-byte Base64Url).
   - `ExpiresAt` (DateTimeOffset, 5-minute window from creation).
   - `IsExpired(DateTimeOffset now)` helper.
2. Create `IPasswordResetTokenRepository` interface:
   - `SaveAsync(UserId userId, PasswordResetToken token, CancellationToken ct)`
   - `FindByTokenAsync(string token, CancellationToken ct) → Task<(UserId, PasswordResetToken)?>`
   - `DeleteByUserIdAsync(UserId userId, CancellationToken ct)` — clears existing tokens before issuing a new one.
3. Add `User.ResetPassword(PasswordHash newHash)` method that updates the credential and raises a `PasswordReset` domain event.

### Application Layer (`SocialDDD.Application`)

1. Add `RequestPasswordResetCommand { Email }` and handler:
   - Look up the user by email (silently succeed even if not found — don't reveal whether the email exists).
   - Delete any existing reset token for that user.
   - Generate a new `PasswordResetToken` with 5-minute expiry.
   - Save the token.
   - Send email via `IEmailService.SendPasswordResetEmailAsync(email, token, ct)` (add this method to the interface).
2. Add `ResetPasswordCommand { Token, NewPassword }` and handler:
   - Look up `(userId, tokenRecord)` by the token string.
   - Validate the token is not expired.
   - Validate `NewPassword` meets strength requirements (min 8 chars — same rules as registration).
   - Hash the new password.
   - Call `user.ResetPassword(newHash)`.
   - Delete the used token.
   - Return success (no auto-login — user must log in explicitly after reset).

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `InMemoryPasswordResetTokenRepository` for development.
2. Implement `MongoDbPasswordResetTokenRepository` (collection `password_reset_tokens`, TTL index on `expiresAt`).
3. Add `SendPasswordResetEmailAsync` to `ConsoleEmailService` (log the token/URL to console).

### API Layer (`SocialDDD.Api`)

1. `POST /api/password-reset-requests` — accepts `{ email }`, always returns 202 Accepted (no information leak).
2. `POST /api/password-resets` — accepts `{ token, newPassword }`:
   - 200 on success.
   - 400 if token is expired, invalid, or password is too weak.

### Tests

1. Unit test `PasswordResetToken` expiry.
2. Unit test `User.ResetPassword` raises `PasswordReset` event.
3. Unit test request handler: non-existent email still returns success (no leak).
4. Unit test reset handler: expired token → error, invalid token → error, valid token → password updated.
5. Unit test that a token can only be used once (second use returns error).

## Acceptance Criteria

- `POST /api/password-reset-requests` always returns 202, console shows the token.
- `POST /api/password-resets` with valid token within 5 min → 200, user can now log in with new password.
- `POST /api/password-resets` used a second time with the same token → 400.
- `POST /api/password-resets` with expired token → 400.
- Old password no longer works after reset.
