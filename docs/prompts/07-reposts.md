# Prompt 07 — Reposts (Shares with Optional Commentary)

## Context

Users should be able to repost another user's post, optionally adding commentary (a "quote repost"). Rules:
- A user cannot repost their own post.
- A user cannot repost the same post more than once.
- A repost with commentary is capped at 280 characters.
- A user can delete their own repost.
- Reposts appear in the feed as a new post that references the original.

## Task

### Domain Layer (`SocialDDD.Domain`)

Treat a repost as a specialized `SocialPost` (share the aggregate, add an `OriginalPostId` field):

1. Add `OriginalPostId PostId?` to `SocialPost`. Null for original posts; set to the original's ID for reposts.
2. Add `SocialPost.CreateRepost(PostId originalPostId, Handle reposterHandle, string? commentary)` factory:
   - `reposterHandle` must differ from the original post's author handle — throw `DomainException("Cannot repost your own post")`.
   - `commentary` is optional; if provided, must be ≤ 280 chars.
   - Sets `OriginalPostId`.
   - Raises `PostReposted { OriginalPostId, ReposterHandle }` domain event.
3. Update `IPostRepository`:
   - Add `Task<SocialPost?> FindRepostAsync(PostId originalPostId, Handle reposterHandle, CancellationToken ct)`.
   - Add `Task<int> GetRepostCountAsync(PostId originalPostId, CancellationToken ct)`.

### Application Layer (`SocialDDD.Application`)

1. Add `CreateRepostCommand { OriginalPostId, ReposterHandle, Commentary? }` and handler:
   - Load the original post; return error if not found or deleted.
   - Verify requester is not the original author.
   - Check that a repost doesn't already exist for `(originalPostId, reposterHandle)`.
   - Call `SocialPost.CreateRepost(...)`.
   - Save and return the repost DTO.
2. Add `DeleteRepostCommand { OriginalPostId, RequesterHandle }` and handler:
   - Find the repost by `(originalPostId, requesterHandle)`.
   - Return error if not found.
   - Delete (soft-delete: set `IsDeleted = true`).
3. Update `PostDto` to include `originalPostId` and `repostCount`.
4. When hydrating a repost DTO, embed the original post's details as `originalPost: PostDto` (a single extra lookup).

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Update MongoDB BSON mapping to persist `OriginalPostId`.
2. Add a compound index on `{ originalPostId, authorId }` for efficient repost-duplicate detection.
3. Implement `FindRepostAsync` using that index.
4. Implement `GetRepostCountAsync` using a `countDocuments` query on `{ originalPostId: id, isDeleted: false }`.

### API Layer (`SocialDDD.Api`)

1. `POST /api/posts/{postId}/reposts` (authenticated) — create a repost. Accepts `{ commentary? }`. Returns 201.
2. `DELETE /api/posts/{postId}/reposts/mine` (authenticated) — delete the caller's repost of this post. Returns 200.
3. Update feed and post-detail responses to include `repostCount` and `isRepostedByMe`.

### Tests

1. Unit test `CreateRepost`: self-repost throws.
2. Unit test `CreateRepost`: commentary over 280 chars throws.
3. Unit test that `OriginalPostId` is correctly set on the repost.
4. Unit test that a deleted post cannot be reposted.

## Acceptance Criteria

- `POST /api/posts/{id}/reposts` creates a repost visible in the feed.
- Self-repost returns 400 with a clear error.
- Duplicate repost of the same post returns 409 Conflict.
- `DELETE /api/posts/{id}/reposts/mine` removes the repost.
- Feed items include `repostCount` and `isRepostedByMe`.
- Repost DTO includes the embedded original post details.
