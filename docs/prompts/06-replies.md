# Prompt 06 — Post Replies (Threaded Conversations)

## Context

Users should be able to reply to posts, creating threaded conversations. A reply is itself a `SocialPost` with a `ParentPostId` set. The reply auto-prefixes its content with `@{parentAuthorHandle}` when displayed. The post detail view should show a conversation tree up to 3 levels deep.

## Task

### Domain Layer (`SocialDDD.Domain`)

1. Add `ParentPostId PostId?` property to `SocialPost`. Null means it is a root post.
2. Update `SocialPost.Create` factory (or add a `SocialPost.CreateReply` factory) to accept an optional `parentPostId`:
   - Validates that content is not empty and within 280 chars.
   - Sets `ParentPostId`.
   - Raises `PostCreated` domain event (already exists; update payload if needed).
3. Add `Mentions HashSet<Handle>` to `SocialPost` — auto-extracted from content (any `@handle` pattern).
4. Add `Hashtags HashSet<string>` to `SocialPost` — auto-extracted from content (`#tag` patterns).
5. Update `IPostRepository`:
   - Add `Task<IReadOnlyList<SocialPost>> GetRepliesAsync(PostId parentPostId, int limit, CancellationToken ct)`.
   - Add `Task<IReadOnlyList<SocialPost>> GetConversationAsync(PostId rootPostId, int depthLimit, int repliesPerLevel, CancellationToken ct)`.

### Application Layer (`SocialDDD.Application`)

1. Add `CreateReplyCommand { ParentPostId, AuthorHandle, Content }` and handler:
   - Load the parent post; return error if not found or deleted.
   - Auto-prefix content with `@{parentAuthorHandle} ` if not already present.
   - Create the reply post via `SocialPost.CreateReply(...)`.
   - Save and return the new post DTO.
2. Add `GetPostWithConversationQuery { PostId, DepthLimit = 3, RepliesPerLevel = 100 }` and handler:
   - Load the root post.
   - Recursively load replies up to `DepthLimit` levels, up to `RepliesPerLevel` per level.
   - Return a `PostConversationDto` tree structure:
     ```
     PostConversationDto {
       Post: PostDto,
       Replies: List<PostConversationDto>
     }
     ```
3. Update `PostDto` to include `parentPostId`, `replyCount`, `mentions`, and `hashtags`.
4. Update `GetFeedQuery` to support filtering: root posts only vs all posts.

### Infrastructure Layer (`SocialDDD.Infrastructure`)

1. Update MongoDB BSON mapping to persist `ParentPostId`, `Mentions`, and `Hashtags`.
2. Add a MongoDB index on `parentPostId` for efficient reply lookups.
3. Implement `GetRepliesAsync` using a simple `{ parentPostId: postId }` query.
4. Implement `GetConversationAsync` with iterative level-by-level loading (avoid recursive DB calls when possible — load all descendants of a root post and assemble the tree in memory for shallow trees).

### API Layer (`SocialDDD.Api`)

1. `POST /api/posts/{postId}/replies` (authenticated) — create a reply. Accepts `{ content }`. Returns 201 with the new reply post.
2. `GET /api/posts/{postId}` — update to return the conversation tree (existing endpoint, enhanced response).
3. Update `GET /api/posts/feed` to accept optional `?rootOnly=true` query param.

### Tests

1. Unit test mention extraction: `"Hello @alice and @BOB"` → `["alice", "bob"]`.
2. Unit test hashtag extraction: `"Love #DDD and #csharp!"` → `["ddd", "csharp"]`.
3. Unit test `CreateReply` auto-prefixes the parent author mention.
4. Unit test that a deleted post cannot be replied to.

## Acceptance Criteria

- `POST /api/posts/{id}/replies` creates a reply linked to the parent.
- `GET /api/posts/{id}` returns the post with up to 3 levels of nested replies.
- Feed can be filtered to root posts only (`?rootOnly=true`).
- Reply content is auto-prefixed with `@parentAuthorHandle`.
- Mentions and hashtags are extracted and stored on every post/reply.
