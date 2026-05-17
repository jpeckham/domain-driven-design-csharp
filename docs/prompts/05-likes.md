# Prompt 05 — Post Likes

## Context

Users should be able to like and unlike posts. A user can only like a given post once. Likes are stored as a relationship between a user (by handle) and a post. The like count should be derivable and the list of who liked a post should be queryable.

## Task

### Domain Layer (`SocialDDD.Domain`)

**Option A — Likes as part of the Post aggregate:**
Add a `LikedBy` collection (`HashSet<Handle>`, case-insensitive) directly on the `SocialPost` aggregate:
- `Post.Like(Handle byHandle)` — adds the handle; throws if already liked.
- `Post.Unlike(Handle byHandle)` — removes the handle; throws if not liked.
- Raise `PostLiked` and `PostUnliked` domain events.
- Expose `LikeCount` as a computed property.

**Option B — Separate Like aggregate:**
If the post collection would grow too large, treat likes separately. For this prompt, use **Option A** (embed in Post) since it is simpler and consistent with the reference app.

1. Add `LikedBy HashSet<Handle>` to `SocialPost` (case-insensitive comparer).
2. Add `Post.Like(Handle byHandle)` and `Post.Unlike(Handle byHandle)` methods.
3. Add `PostLiked { PostId, LikedByHandle }` and `PostUnliked { PostId, UnlikedByHandle }` domain events.
4. Update `IPostRepository`:
   - Add `Task AddLikeAsync(PostId postId, Handle handle, CancellationToken ct)` (atomic upsert on the set).
   - Add `Task RemoveLikeAsync(PostId postId, Handle handle, CancellationToken ct)`.
   - Add `Task<bool> IsLikedByAsync(PostId postId, Handle handle, CancellationToken ct)`.

### Application Layer (`SocialDDD.Application`)

1. Add `LikePostCommand { PostId, RequesterHandle }` and handler:
   - Load the post; return error if not found or deleted.
   - Check user has not already liked it.
   - Call `post.Like(handle)` and save.
2. Add `UnlikePostCommand { PostId, RequesterHandle }` and handler:
   - Load the post; return error if not found or deleted.
   - Check user has liked it.
   - Call `post.Unlike(handle)` and save.
3. Update post response DTOs to include `likeCount` and `likedByMe` (requires the requester's handle to compute `likedByMe`).

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Update MongoDB BSON mapping to persist `LikedBy` as an array field.
2. Add a MongoDB index on the `likedBy` field for efficient per-user queries.
3. Implement `AddLikeAsync` using MongoDB's `$addToSet` operator (idempotent).
4. Implement `RemoveLikeAsync` using MongoDB's `$pull` operator.

### API Layer (`SocialDDD.Api`)

1. `POST /api/posts/{postId}/likes` (authenticated) — like a post. Returns 200 with updated like count.
2. `DELETE /api/posts/{postId}/likes` (authenticated) — unlike a post. Returns 200 with updated like count.
3. Update feed and post-detail responses to include `likeCount` and `likedByMe`.

### Tests

1. Unit test `Post.Like`: first like succeeds; second like by same user throws.
2. Unit test `Post.Unlike`: unlike after like succeeds; unlike without prior like throws.
3. Unit test that `LikeCount` reflects the set size correctly.

## Acceptance Criteria

- `POST /api/posts/{id}/likes` adds a like; second call returns 409 Conflict.
- `DELETE /api/posts/{id}/likes` removes the like; call without prior like returns 404.
- Feed response includes `likeCount` and `likedByMe: true/false` per post.
- Likes persist across server restarts (MongoDB).
