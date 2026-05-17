# Prompt 13 — Frontend: Auth Flows (Verification, MFA, Password Reset)

## Context

The Blazor WebAssembly frontend currently has `Login.razor` and `Register.razor`. After prompts 02–04, the backend now supports email verification, device MFA, and password reset. This prompt adds the corresponding Blazor pages.

All new pages should follow the same visual style as the existing `Login.razor` and `Register.razor`.

## Task

### Update Existing Pages

1. **`Register.razor`** — Change to call `POST /api/registrations` instead of `POST /api/users/register`:
   - Add `handle` and `displayName` fields to the form.
   - On success (202), navigate to `/verify-registration?email={email}`.

2. **`Login.razor`** — After prompt 03, login may return 202 (OTP required). Handle this:
   - Add a `deviceId` field (generate once per browser session and store in `localStorage`).
   - If response is 202, navigate to `/verify-device?email={email}&deviceId={deviceId}`.

### New Pages

3. **`VerifyRegistration.razor`** (`/verify-registration`):
   - Accepts `?email=` query param (pre-fills the email field, read-only).
   - Form: email (read-only), 6-digit code input.
   - On submit, call `POST /api/registrations/verify`.
   - On success (200 with token), store token in `AuthService` and navigate to `/feed`.
   - On error, show inline error message.
   - "Resend code" button (calls `POST /api/registrations` again with the same email).

4. **`VerifyDevice.razor`** (`/verify-device`):
   - Accepts `?email=` and `?deviceId=` query params.
   - Form: 6-digit OTP input + "Remember this device" checkbox.
   - On submit, call `POST /api/sessions/device/verify`.
   - On success, store token and navigate to `/feed`.
   - On error, show inline error.

5. **`ForgotPassword.razor`** (`/forgot-password`):
   - Form: email input.
   - On submit, call `POST /api/password-reset-requests`.
   - Always show a success message ("If that email is registered, you'll receive a link shortly").
   - Link back to `/login`.

6. **`ResetPassword.razor`** (`/reset-password`):
   - Accepts `?token=` query param.
   - Form: new password + confirm password inputs.
   - Validate passwords match client-side.
   - On submit, call `POST /api/password-resets`.
   - On success, show "Password updated — please log in" and navigate to `/login` after 2 seconds.
   - On error, show error message.

### API Client Updates (`SocialAppApiClient` or equivalent)

Add methods:
- `RegisterPendingAsync(handle, displayName, email, password)`
- `VerifyRegistrationAsync(email, code)`
- `LoginWithDeviceAsync(email, password, deviceId)`
- `VerifyDeviceOtpAsync(email, deviceId, otp, rememberDevice)`
- `RequestPasswordResetAsync(email)`
- `ResetPasswordAsync(token, newPassword)`

### Navigation

- Add "Forgot password?" link to `Login.razor`.
- Add "Already have an account? Log in" link to `Register.razor`.
- Add "Resend code" to `VerifyRegistration.razor`.

## Acceptance Criteria

- Full registration flow: Register → receive code in console → enter code → land on feed.
- Full MFA flow: Login with unknown device → receive OTP in console → enter OTP → land on feed.
- Login with remembered device: Login → land on feed immediately (no OTP).
- Forgot password flow: enter email → console shows token → `/reset-password?token=...` → set new password → login works.
- All error states show inline messages without page reload.
- Device ID is persisted across page refreshes (stored in `localStorage`).
