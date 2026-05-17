# Prompt 17 — Frontend: Feed Enhancements (Search, Following Feed, Infinite Scroll)

## Context

After prompts 08, 09, and 12, the backend supports follows, blocks, and post search. This prompt enhances the `Feed.razor` page with a following-only toggle, post search, and infinite scroll.

## Task

### Update `Feed.razor`

1. **Feed mode toggle** (tabs or toggle buttons at the top):
   - "All posts" — loads `GET /api/posts/feed`.
   - "Following" — loads `GET /api/posts/feed?followingOnly=true`. Only visible when logged in.

2. **Search bar**:
   - A text input with a search icon at the top of the feed.
   - On submit (Enter or button click), calls `GET /api/posts/search?q={query}`.
   - Shows search results in place of the regular feed.
   - Shows the query as a label (e.g. "Results for: hello").
   - An "×" clear button dismisses the search and returns to the regular feed.
   - URL should reflect the search state (`/feed?q=hello`) so the link is shareable.

3. **Infinite scroll / "Load more"**:
   - Replace the current paginated feed with either:
     - An "Load more" button at the bottom (simpler), or
     - Intersection Observer–based auto-load (preferred — use JS interop with a sentinel `<div>` at the bottom).
   - Append new posts below existing ones on load (don't replace the list).
   - Track the `offset` value client-side; increment by `limit` (20) on each load.
   - Hide the "Load more" button / stop auto-loading when the server returns fewer posts than `limit` (indicates end of feed).

4. **Block from feed**:
   - Add a "⋯" (more options) menu to each `PostCard` in the feed.
   - Option: "Block @{handle}" — calls `POST /api/users/{handle}/blocks`; removes that user's posts from the current feed immediately (client-side filter).

### New Page: `SearchResults.razor` (`/search?q=`)

Alternatively, search results can render inline in `Feed.razor` (show a `SearchResults` component instead of the feed list). Either approach is acceptable — document your choice.

- Shows post results for the search query.
- Reuses `PostCard` components.
- Shows "No results found" when empty.
- Paginated with "Load more".

### New Page: `UserSearch.razor` (`/search/users?q=`) — Optional

A separate page to search for users by handle or display name (requires a backend `GET /api/users/search?q=` endpoint):
- If implementing, add a simple `GET /api/users/search?q=` endpoint to the API and a corresponding repository method.
- Results show `AuthorThumbnail` + follow button.

### API Client Updates

Add methods:
- `SearchPostsAsync(query, limit, offset)`
- (optional) `SearchUsersAsync(query, limit, offset)`

## Acceptance Criteria

- "Following" tab shows only posts from followed users; "All" tab shows all posts.
- Typing a query and submitting shows search results; clearing returns to feed.
- Feed auto-loads more posts when the user scrolls to the bottom (or "Load more" button works).
- Blocking a user from the feed's "⋯" menu removes their posts immediately.
- URL updates to `/feed?q=hello` during search (shareable link restores the search state on load).
- "End of feed" indicator shown when no more posts are available.
