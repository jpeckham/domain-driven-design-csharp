# Prompt 10 — Profile Image Upload

## Context

Users should be able to upload, view, and remove a profile image. The upload uses a two-step flow to support large files without blocking the API:

1. Client requests an upload slot → receives an `assetId` and an upload URL.
2. Client PUTs the file bytes to the upload URL.
3. Client calls a "complete" endpoint → the image is committed to the user profile.

Profile images are served via a dedicated endpoint.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Create a `ProfileImage` value object (embedded in `User`):
   - `AssetId Guid`
   - `StorageKey string`
   - `ContentType string` (e.g. `image/jpeg`)
   - `ByteLength long`
   - `Width int?`
   - `Height int?`
   - `UploadedAt DateTimeOffset`
2. Add `ProfileImage? ProfileImage` property to the `User` aggregate.
3. Add `User.SetProfileImage(ProfileImage image)` method — raises `ProfileImageUpdated` domain event.
4. Add `User.RemoveProfileImage()` method — raises `ProfileImageRemoved` domain event. Throws if no image set.
5. Create `IProfileImageStorageService` interface:
   - `Task<(string uploadUrl, string storageKey)> ReserveUploadAsync(Guid assetId, string contentType, CancellationToken ct)`
   - `Task StoreAsync(Guid assetId, string storageKey, Stream data, string contentType, CancellationToken ct)`
   - `Task<Stream> LoadAsync(string storageKey, CancellationToken ct)`
   - `Task DeleteAsync(string storageKey, CancellationToken ct)`

### Application Layer (`SocialDDD.Application`)

1. Add `BeginProfileImageUploadCommand { UserHandle, ContentType }` and handler:
   - Validate `ContentType` is an accepted image type (`image/jpeg`, `image/png`, `image/webp`).
   - Generate `assetId = Guid.NewGuid()`.
   - Call `IProfileImageStorageService.ReserveUploadAsync` to get the upload URL and storage key.
   - Return `{ assetId, uploadUrl }` to the client.
2. Add `CompleteProfileImageUploadCommand { UserHandle, AssetId, ContentType, ByteLength, Width?, Height? }` and handler:
   - Load the user.
   - Build a `ProfileImage` value object.
   - If user already has a profile image, call `IProfileImageStorageService.DeleteAsync` on the old storage key.
   - Call `user.SetProfileImage(image)`.
   - Save the user.
3. Add `RemoveProfileImageCommand { UserHandle }` and handler:
   - Load the user; return error if no profile image set.
   - Call `IProfileImageStorageService.DeleteAsync`.
   - Call `user.RemoveProfileImage()`.
   - Save.
4. Add `GetProfileImageQuery { AssetId }` and handler:
   - Look up the user who owns this asset ID.
   - Call `IProfileImageStorageService.LoadAsync`.
   - Return the stream + content type.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `LocalFileProfileImageStorageService`:
   - Stores files under a configurable directory (e.g. `./data/profile-images/`).
   - `ReserveUploadAsync` returns a local upload URL pattern: `/api/media/uploads/profile/{assetId}`.
   - `StoreAsync` writes bytes to disk.
   - `LoadAsync` reads from disk.
   - `DeleteAsync` removes the file.
2. Implement `AzureBlobProfileImageStorageService` stub (reads connection string from config; uses Azure Blob SDK for real uploads).
3. Update `UserRepository` MongoDB mapping to persist the embedded `ProfileImage`.

### API Layer (`SocialDDD.Api`)

1. `POST /api/users/me/profile-image/upload-sessions` (authenticated) — begin upload. Accepts `{ contentType }`. Returns `{ assetId, uploadUrl }`.
2. `PUT /api/media/uploads/profile/{assetId}` — receive raw file bytes (no auth required for the PUT itself, since the URL is the token). Calls `IProfileImageStorageService.StoreAsync`.
3. `POST /api/users/me/profile-image/complete` (authenticated) — complete upload. Accepts `{ assetId, contentType, byteLength, width?, height? }`. Returns updated user profile.
4. `DELETE /api/users/me/profile-image` (authenticated) — remove profile image.
5. `GET /api/profile-images/{assetId}` — serve the image bytes with correct `Content-Type` header.

### Tests

1. Unit test `User.SetProfileImage` raises correct domain event.
2. Unit test `User.RemoveProfileImage` on a user with no image throws.
3. Integration test (with local file service) for the full upload → complete → serve flow.

## Acceptance Criteria

- Full upload flow works: begin → PUT bytes → complete → `GET /api/profile-images/{assetId}` serves the image.
- `DELETE /api/users/me/profile-image` removes the image; subsequent `GET` returns 404.
- Replacing a profile image deletes the old file.
- User profile response includes `profileImageUrl` (or null if none set).
- Only accepted image content types are allowed (jpeg, png, webp).
