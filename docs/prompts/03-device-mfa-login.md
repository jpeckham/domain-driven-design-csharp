# Prompt 03 — Device-Based MFA Login with OTP

## Context

The current login flow is simple: email + password → JWT. The target feature adds optional multi-factor authentication via a device-tracking + OTP email flow:

1. Client sends `{ email, password, deviceId }`.
2. If the device is already "remembered" for that user → issue token immediately (no OTP).
3. If the device is unknown → send a 10-minute OTP to the user's email and return 202.
4. Client submits `{ email, deviceId, otp }` to verify.
5. On success → issue token; optionally mark the device as "remembered".

`deviceId` is a client-generated stable identifier (e.g. a UUID stored in localStorage).

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Create a `DeviceId` value object (wraps a non-empty string GUID).
2. Create a `OneTimePasscode` value object:
   - 6-digit numeric string.
   - `ExpiresAt` (DateTimeOffset, 10-minute window).
   - `IsExpired(DateTimeOffset now)` helper.
3. Create `IRememberedDeviceRepository` interface:
   - `IsRememberedAsync(UserId userId, DeviceId deviceId, CancellationToken ct) → Task<bool>`
   - `RememberAsync(UserId userId, DeviceId deviceId, CancellationToken ct)`
4. Create `IOtpRepository` interface:
   - `SaveAsync(UserId userId, DeviceId deviceId, OneTimePasscode otp, CancellationToken ct)`
   - `FindAsync(UserId userId, DeviceId deviceId, CancellationToken ct) → Task<OneTimePasscode?>`
   - `DeleteAsync(UserId userId, DeviceId deviceId, CancellationToken ct)`

### Application Layer (`SocialDDD.Application`)

1. Add `LoginWithDeviceCommand { Email, Password, DeviceId }` and handler:
   - Validate credentials (same as existing login).
   - If device is remembered → return token immediately (`LoginWithDeviceResult.Success`).
   - If device is unknown → generate OTP, save it, send email, return `LoginWithDeviceResult.OtpRequired`.
2. Add `VerifyDeviceOtpCommand { Email, DeviceId, Otp, RememberDevice }` and handler:
   - Load user by email.
   - Load the OTP for `(userId, deviceId)`.
   - Validate OTP matches and is not expired.
   - Delete the used OTP.
   - If `RememberDevice` is true → call `IRememberedDeviceRepository.RememberAsync`.
   - Return a bearer token.
3. Reuse `IEmailService` from prompt 02 — add `SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct)`.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `InMemoryRememberedDeviceRepository` for development.
2. Implement `MongoDbRememberedDeviceRepository` (collection `remembered_devices`).
3. Implement `InMemoryOtpRepository` for development.
4. Implement `MongoDbOtpRepository` (collection `device_otps`, TTL index on `expiresAt`).

### API Layer (`SocialDDD.Api`)

1. Add `POST /api/sessions/device` — accepts `{ email, password, deviceId }`.
   - Returns 200 with token if device is remembered.
   - Returns 202 Accepted (no body) if OTP was sent.
   - Returns 401 on bad credentials.
2. Add `POST /api/sessions/device/verify` — accepts `{ email, deviceId, otp, rememberDevice }`.
   - Returns 200 with token on success.
   - Returns 400 on wrong/expired OTP.
3. Keep existing `POST /api/sessions` (simple login) unchanged.

### Tests

1. Unit test `OneTimePasscode` expiry.
2. Unit test login-with-device handler: known device returns token, unknown device returns OtpRequired.
3. Unit test verify-device handler: wrong OTP returns error, expired OTP returns error, correct OTP returns token.

## Acceptance Criteria

- `POST /api/sessions/device` with valid creds + unknown device returns 202 and sends OTP to console.
- `POST /api/sessions/device/verify` with correct OTP (within 10 min) returns 200 with bearer token.
- `POST /api/sessions/device` with valid creds + a previously remembered device returns 200 with token directly.
- Expired or incorrect OTP returns 400.
