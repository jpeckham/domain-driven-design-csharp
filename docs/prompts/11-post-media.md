# Prompt 11 — Post Media Uploads (Images and Video)

## Context

Posts should support attaching media: up to 4 images **or** 1 video (not mixed). Media uses the same two-step upload pattern as profile images (reserve → PUT bytes → complete). Media is served via a dedicated endpoint.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Create a `MediaKind` enum: `Image`, `Video`.
2. Create a `PostMedia` value object:
   - `AssetId Guid`
   - `Kind MediaKind`
   - `StorageKey string`
   - `ContentType string`
   - `ByteLength long`
   - `Width int?`
   - `Height int?`
   - `DurationMs long?` (for video)
   - `ThumbnailKey string?` (for video thumbnails)
   - `AltText string?` (accessibility)
   - `SortOrder int`
3. Add `IReadOnlyList<PostMedia> Media` to `SocialPost`.
4. Add a `SocialPost.AttachMedia(IReadOnlyList<PostMedia> media)` method (called during create):
   - Max 4 items.
   - All items must be the same `Kind`.
   - Throw `DomainException` on violation.
5. Update `SocialPost.Create` / `CreateReply` to accept optional `IReadOnlyList<PostMedia> media`.
6. Create `IPostMediaStorageService` interface (mirrors `IProfileImageStorageService` but namespaced for posts).

### Application Layer (`SocialDDD.Application`)

1. Add `BeginPostMediaUploadCommand { UserHandle, ContentType }` and handler:
   - Validate content type is accepted (`image/jpeg`, `image/png`, `image/webp`, `image/gif`, `video/mp4`).
   - Return `{ assetId, uploadUrl }`.
2. Add `CompletePostMediaUploadCommand { AssetId, ContentType, ByteLength, Width?, Height?, DurationMs?, AltText? }` and handler:
   - Validate the asset was reserved (track pending uploads in a short-lived in-memory store keyed by assetId).
   - Return a `PendingMediaDto` (not yet attached to a post — the client collects these and passes them at post-creation time).
3. Update `CreatePostCommand` and `CreateReplyCommand` to accept `IReadOnlyList<PendingMediaAssetId>` (list of committed asset IDs):
   - Load each committed `PostMedia` from the pending store.
   - Pass to `SocialPost.Create(...)`.
   - Validate the 4-image / 1-video rule at the application layer before calling the domain.
4. Update `PostDto` to include the `media` list (each item includes the asset ID, kind, dimensions, alt text, a served URL).

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `LocalFilePostMediaStorageService` (same pattern as profile images, different directory).
2. Implement `InMemoryPendingMediaStore` — tracks `(assetId → PostMedia)` pairs with a short TTL (1 hour).
3. Update MongoDB BSON mapping to persist the `Media` list inside each `SocialPost` document.

### API Layer (`SocialDDD.Api`)

1. `POST /api/posts/media/upload-sessions` (authenticated) — begin media upload. Accepts `{ contentType }`. Returns `{ assetId, uploadUrl }`.
2. `PUT /api/media/uploads/post/{assetId}` — receive raw file bytes.
3. `POST /api/posts/media/{assetId}/complete` (authenticated) — complete media upload. Accepts `{ contentType, byteLength, width?, height?, durationMs?, altText? }`. Returns `{ assetId }`.
4. Update `POST /api/posts` to accept optional `mediaAssetIds: string[]` in the request body.
5. `GET /api/post-media/{assetId}` — serve the media bytes with correct `Content-Type`.

### Tests

1. Unit test `SocialPost.AttachMedia`: 5 images throws, mixed kinds throws, 4 images passes, 1 video passes.
2. Unit test that a post with no media is still valid.
3. Integration test: upload image → complete → create post with asset ID → `GET /api/post-media/{assetId}` serves the image.

## Acceptance Criteria

- Post creation with up to 4 images works end-to-end.
- Post creation with 1 video works end-to-end.
- Mixing images and video in one post returns 400.
- More than 4 images in one post returns 400.
- Post feed response includes media list with served URLs.
- `GET /api/post-media/{assetId}` returns the correct bytes and `Content-Type`.
