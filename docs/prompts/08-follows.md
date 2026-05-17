# Prompt 08 — Follow / Unfollow Users

## Context

Users should be able to follow other users. Following affects the feed: an optional "following only" feed mode shows posts from accounts the user follows. The followers/following lists should be viewable on user profiles.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Create a `Follow` aggregate root:
   - `FollowId` (value object, wraps a GUID).
   - `FollowerHandle Handle` — who is following.
   - `FolloweeHandle Handle` — who is being followed.
   - `FollowedAt DateTimeOffset`.
   - Factory: `Follow.Create(Handle follower, Handle followee)` — throws `DomainException` if `follower == followee`.
   - Raises `UserFollowed { FollowerHandle, FolloweeHandle }` domain event.
2. Create `IFollowRepository` interface:
   - `SaveAsync(Follow follow, CancellationToken ct)`
   - `DeleteAsync(Handle follower, Handle followee, CancellationToken ct)`
   - `FindAsync(Handle follower, Handle followee, CancellationToken ct) → Task<Follow?>`
   - `GetFolloweesAsync(Handle follower, CancellationToken ct) → Task<IReadOnlyList<Handle>>`
   - `GetFollowersAsync(Handle followee, CancellationToken ct) → Task<IReadOnlyList<Handle>>`
   - `GetFolloweeCountAsync(Handle follower, CancellationToken ct) → Task<int>`
   - `GetFollowerCountAsync(Handle followee, CancellationToken ct) → Task<int>`

### Application Layer (`SocialDDD.Application`)

1. Add `FollowUserCommand { FollowerHandle, FolloweeHandle }` and handler:
   - Verify the followee user exists.
   - Verify not already following.
   - Create and save the `Follow` aggregate.
2. Add `UnfollowUserCommand { FollowerHandle, FolloweeHandle }` and handler:
   - Find the follow record; return error if not found.
   - Delete it.
3. Add `GetUserProfileQuery { Handle, RequesterHandle? }` and handler (creates/replaces the existing profile endpoint):
   - Return user details + follower count + following count + `isFollowedByMe` (if requester provided).
4. Update `GetFeedQuery` to accept `?followingOnly=true`:
   - If true, load the requester's followees and filter the post query to those handles.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement `InMemoryFollowRepository` for development.
2. Implement `MongoDbFollowRepository`:
   - Collection `follows`.
   - Compound unique index on `{ followerHandle, followeeHandle }`.
   - Index on `followerHandle` for followee list queries.
   - Index on `followeeHandle` for follower list queries.

### API Layer (`SocialDDD.Api`)

1. `POST /api/users/{handle}/follows` (authenticated) — follow a user. Returns 200.
2. `DELETE /api/users/{handle}/follows` (authenticated) — unfollow. Returns 200.
3. `GET /api/users/{handle}/followers` — paginated list of followers.
4. `GET /api/users/{handle}/following` — paginated list of accounts being followed.
5. Update `GET /api/users/{handle}` to include `followerCount`, `followingCount`, and `isFollowedByMe`.
6. Update `GET /api/posts/feed` to support `?followingOnly=true`.

### Tests

1. Unit test `Follow.Create`: self-follow throws.
2. Unit test that following the same user twice is rejected at the application layer.
3. Unit test feed filtering by followees returns only those users' posts.

## Acceptance Criteria

- `POST /api/users/alice/follows` creates a follow relationship.
- Self-follow returns 400.
- Duplicate follow returns 409.
- `GET /api/users/alice` shows correct `followerCount` and `isFollowedByMe`.
- `GET /api/posts/feed?followingOnly=true` returns only posts from followed users.
- `GET /api/users/alice/followers` and `/following` return the correct lists.
