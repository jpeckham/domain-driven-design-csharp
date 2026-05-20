# Domain Event Email Handlers Design

## Goal

Move email side effects for user registration, password reset requests, and unknown-device login challenges behind domain event handlers so commands no longer call `IEmailService` directly.

## Architecture

Add a generic `IDomainEventHandler<TEvent>` abstraction in Application and update the Infrastructure dispatcher to resolve registered handlers from DI. Event handlers live in Infrastructure because they use email and persistence dependencies. Commands keep the state-changing work, then dispatch events that describe the email side effect to perform.

`UserRegistered` carries enough registration data for a verification handler. Password reset and login challenge commands dispatch new events after they save their token or OTP. The dispatcher enqueues handler work onto a background task with a fresh DI scope, so the request path is not blocked by email delivery.

## Components

- `IDomainEventHandler<TEvent>`: handler contract for typed domain events.
- `DomainEventDispatcher`: resolves all handlers registered for each event type and runs them in a background scope.
- `SendUserRegisteredVerificationEmailHandler`: creates and stores a verification code, then sends the verification email.
- `SendPasswordResetRequestedEmailHandler`: sends the password reset token email.
- `SendLoginChallengedEmailHandler`: sends the unknown-device OTP email.

## Data Flow

Registration creates a pending user, saves it, and dispatches `UserRegistered`. The handler creates the verification code and sends the email.

Password reset request validates the email, creates and saves a one-time reset token, then dispatches `PasswordResetRequested`. The handler sends the reset email.

Unknown-device login validates credentials, saves the OTP, then dispatches `LoginChallenged`. The handler sends the OTP email.

## Error Handling

Invalid or unknown password reset requests still return silently. Handler failures are logged by the dispatcher and do not fail the initiating request after dispatch has accepted the event.

## Testing

Tests cover event-handler side effects, command dispatch behavior, and dispatcher resolution of registered handlers.
