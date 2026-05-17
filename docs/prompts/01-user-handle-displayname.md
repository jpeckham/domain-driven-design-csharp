# Prompt 01 — Enhance User Aggregate: Handle + DisplayName

## Context

The DDD social app currently identifies users by a `UserId` (GUID) and stores only `Username`, `Email`, and `PasswordHash`. The target feature set requires:

- A **handle** — a URL-safe, case-insensitive, unique identifier (e.g. `@alice`). Used in mentions, profile URLs, and post attribution.
- A **display name** — a human-readable name shown in the UI (e.g. "Alice Smith"). Distinct from the handle.

This mirrors Twitter/X-style identity: the handle is immutable or rarely changed; the display name is freely editable.

## Task

Evolve the `User` aggregate and all dependent layers to add `Handle` and `DisplayName`.

### Domain Layer (`SocialDDD.Domain`)

1. Create a `Handle` value object:
   - Wraps a `string`.
   - Valid characters: letters, digits, underscores (no spaces, no `@` prefix stored).
   - Length: 1–30 characters.
   - Stored and compared case-insensitively (normalize to lowercase on construction).
   - Expose `Value` (normalized) and `Display` (`@` + Value).
   - Throw `DomainException` on invalid input.

2. Create a `DisplayName` value object:
   - Wraps a `string`.
   - Length: 1–50 characters, trimmed.
   - Throw `DomainException` on invalid input.

3. Update `User` aggregate root:
   - Add `Handle Handle` and `DisplayName DisplayName` properties.
   - Update the `Register` factory method signature to accept `handle` and `displayName`.
   - Raise the existing `UserRegistered` domain event (add handle/displayName to its payload if needed).
   - Add a `UpdateDisplayName(DisplayName newName)` method for future profile editing.

4. Update `IUserRepository`:
   - Add `Task<User?> FindByHandleAsync(Handle handle, CancellationToken ct)`.
   - Add `Task<bool> HandleExistsAsync(Handle handle, CancellationToken ct)`.

### Application Layer (`SocialDDD.Application`)

1. Update `RegisterUserCommand` / `RegisterUserRequest` DTO to include `Handle` and `DisplayName` fields.
2. Update `UserService.RegisterAsync`:
   - Validate handle uniqueness (call `HandleExistsAsync`).
   - Return a meaningful error if the handle is taken.
3. Update `UserProfileDto` / response type to expose `Handle` and `DisplayName`.
4. Add `UpdateDisplayNameCommand` and handler that calls `user.UpdateDisplayName(...)` and saves.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Update MongoDB BSON mapping in `UserRepository` to persist `Handle` and `DisplayName`.
2. Implement `FindByHandleAsync` and `HandleExistsAsync` with a case-insensitive index on the `handle` field.
3. Add a unique MongoDB index on `handle` (ensure the migration/startup code creates it).

### API Layer (`SocialDDD.Api`)

1. Update `POST /api/users/register` to accept and validate `handle` and `displayName` in the request body.
2. Update `GET /api/users/{id}` response to include `handle` and `displayName`.
3. Add `GET /api/users/by-handle/{handle}` endpoint that looks up a user by handle.
4. Add `PUT /api/users/me/display-name` endpoint (authenticated) to update display name.

### Tests (`SocialDDD.Domain.Tests`)

1. Add unit tests for the `Handle` value object: valid inputs, invalid characters, length limits, case normalization.
2. Add unit tests for the `DisplayName` value object: valid inputs, length limits, trimming.
3. Add a test verifying `User.Register` produces the correct handle in its domain event.

## Acceptance Criteria

- Registering a user requires a handle and display name.
- Two users cannot share the same handle (case-insensitive).
- `GET /api/users/by-handle/alice` returns the user whose handle is `alice` (or `Alice`, `ALICE`).
- All existing tests still pass.
- MongoDB has a unique index on the handle field.
