# Prompt 09 — Block / Unblock Users

## Context

Users should be able to block other users. Blocking hides the blocked user's posts from the blocker's feed and prevents the blocked user from seeing the blocker's posts. It also removes any existing follow relationship in both directions.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Create a `Block` aggregate root:
   - `BlockId` (value object, wraps a GUID).
   - `BlockerHandle Handle` — who is blocking.
   - `BlockedHandle Handle` — who is being blocked.
   - `BlockedAt DateTimeOffset`.
   - Factory: `Block.Create(Handle blocker, Handle blocked)` — throws `DomainException` if `blocker == blocked`.
   - Raises `UserBlocked { BlockerHandle, BlockedHandle }` domain event.
2. Create `IBlockRepository` interface:
   - `SaveAsync(Block block, CancellationToken ct)`
   - `DeleteAsync(Handle blocker, Handle blocked, CancellationToken ct)`
   - `FindAsync(Handle blocker, Handle blocked, CancellationToken ct) → Task<Block?>`
   - `GetBlockedHandlesAsync(Handle blocker, CancellationToken ct) → Task<IReadOnlyList<Handle>>`
   - `IsBlockedAsync(Handle blocker, Handle blocked, CancellationToken ct) → Task<bool>`

### Application Layer (`SocialDDD.Application`)

1. Add `BlockUserCommand { BlockerHandle, BlockedHandle }` and handler:
   - Verify the blocked user exists.
   - Throw if trying to block yourself.
   - If there is an existing follow in either direction (blocker→blocked or blocked→blocker), remove it via `IFollowRepository`.
   - Create and save the `Block` aggregate.
2. Add `UnblockUserCommand { BlockerHandle, BlockedHandle }` and handler:
   - Find the block record; return error if not found.
   - Delete it.
3. Update `GetFeedQuery` handler:
   - Load the requester's blocked handles.
   - Exclude posts authored by any blocked handle from the feed results.
   - Also exclude posts authored by users who have blocked the requester (requires a reverse check or a denormalized "blocked by" list — implement whichever is simpler; document your choice).
4. Update `GetPostWithConversationQuery`:
   - Filter out replies/reposts from blocked users.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `InMemoryBlockRepository` for development.
2. Implement `MongoDbBlockRepository`:
   - Collection `blocks`.
   - Compound unique index on `{ blockerHandle, blockedHandle }`.
   - Index on `blockerHandle` for efficient "get blocked list" queries.

### API Layer (`SocialDDD.Api`)

1. `POST /api/users/{handle}/blocks` (authenticated) — block a user. Returns 200.
2. `DELETE /api/users/{handle}/blocks` (authenticated) — unblock. Returns 200.
3. `GET /api/users/me/blocks` (authenticated) — list handles the current user has blocked.
4. Update feed and conversation endpoints to respect blocks.

### Tests

1. Unit test `Block.Create`: self-block throws.
2. Unit test that blocking removes existing follows.
3. Unit test feed filtering excludes posts from blocked users.

## Acceptance Criteria

- `POST /api/users/alice/blocks` blocks Alice.
- Self-block returns 400.
- After blocking Alice, Alice's posts do not appear in the blocker's feed.
- `DELETE /api/users/alice/blocks` unblocks Alice; her posts return to the feed.
- Blocking removes follow relationships in both directions.
- `GET /api/users/me/blocks` returns the list of blocked handles.
