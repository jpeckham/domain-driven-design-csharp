# Prompt 12 — Full-Text Post Search

## Context

Users should be able to search posts by text query. Results should be paginated, ordered by relevance (then recency), and exclude deleted posts and posts from blocked users.

## Task

### Domain Layer (`SocialDDD.Domain`)

No new aggregates needed. Update `IPostRepository`:
- Add `Task<IReadOnlyList<SocialPost>> SearchAsync(string query, Handle? requesterHandle, int limit, int offset, CancellationToken ct)`.

### Application Layer (`SocialDDD.Application`)

1. Add `SearchPostsQuery { Query, RequesterHandle?, Limit = 20, Offset = 0 }` and handler:
   - Validate `Query` is not empty and not longer than 200 characters.
   - If `RequesterHandle` is provided, load blocked handles and pass to the repository for exclusion.
   - Call `IPostRepository.SearchAsync(...)`.
   - Map results to `PostDto` list (with `likedByMe`, `isRepostedByMe` etc. if requester is known).
   - Return `SearchResultsDto { Posts: List<PostDto>, Query: string, Limit: int, Offset: int }`.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Implement MongoDB full-text search in `PostRepository.SearchAsync`:
   - Create a MongoDB text index on the `content` field (and optionally `hashtags`): `{ content: "text", hashtags: "text" }`.
   - Use `$text: { $search: query }` with `$meta: "textScore"` projection for relevance sorting.
   - Filter: `{ isDeleted: false }`, exclude blocked authors if provided.
   - Sort by `{ score: { $meta: "textScore" }, createdAt: -1 }`.
   - Apply `limit` and `skip` for pagination.
2. Add startup code to ensure the text index exists (idempotent `CreateIndex` call).

### API Layer (`SocialDDD.Api`)

1. `GET /api/posts/search?q={query}&limit={n}&offset={n}` (authentication optional):
   - If authenticated, pass `requesterHandle` to filter blocked users and compute `likedByMe`.
   - Returns `{ posts: [...], query: "...", limit: 20, offset: 0 }`.
   - Returns 400 if `q` is missing or too long.

### Tests

1. Unit test `SearchPostsQuery` handler rejects empty query.
2. Unit test search results exclude deleted posts.
3. Integration test (with MongoDB): text search finds posts containing the query term; blocked users' posts are excluded.

## Acceptance Criteria

- `GET /api/posts/search?q=hello` returns posts containing "hello" ordered by relevance.
- Empty or missing `q` returns 400.
- Deleted posts are not returned.
- If authenticated, blocked users' posts are excluded from results.
- Results are paginated via `limit` and `offset`.
