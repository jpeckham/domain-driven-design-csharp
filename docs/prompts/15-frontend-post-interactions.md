# Prompt 15 — Frontend: Post Interactions (Like, Reply, Repost)

## Context

After prompts 05–07, the backend supports likes, replies, and reposts. This prompt wires them up in the Blazor frontend — specifically in the feed (`Feed.razor`) and the post detail page.

## Task

### Update `Feed.razor`

1. Each `PostCard` in the feed should show action buttons:
   - **Like button**: heart icon + like count. Filled/highlighted if `likedByMe`. On click, call `POST /api/posts/{id}/likes` (or `DELETE` if already liked). Update count optimistically.
   - **Reply button**: speech bubble icon + reply count. On click, opens the inline reply composer (see below).
   - **Repost button**: retweet-style icon + repost count. Filled if `isRepostedByMe`. On click, opens a confirmation/commentary dialog (see below).
   - **Delete button**: visible only if the post belongs to the logged-in user. On click, confirm then call `DELETE /api/posts/{id}`.

2. **Inline reply composer** (appears below a post card when the reply button is clicked):
   - Textarea (max 280 chars), shows the parent post's author handle pre-mentioned.
   - "Reply" submit button → calls `POST /api/posts/{id}/replies`.
   - On success, insert the new reply above the composer and close the composer.
   - Character counter.

3. **Repost dialog** (a modal or inline panel):
   - Option A: "Repost" (no commentary) → calls `POST /api/posts/{id}/reposts` with empty commentary.
   - Option B: "Quote repost" (shows a textarea for commentary, max 280 chars) → same endpoint with commentary.
   - If `isRepostedByMe`, show "Undo repost" option → calls `DELETE /api/posts/{id}/reposts/mine`.

### New Page: `PostDetail.razor` (`/posts/{postId}`)

Show a single post with its full conversation tree:

1. Load data from `GET /api/posts/{postId}` which returns the post + nested replies.
2. Show the root post at the top (full `PostCard`).
3. Below, show replies indented by level (up to 3 levels deep), each as a `PostCard`.
4. At the bottom, show an inline reply composer (pre-filled with the root author's mention).
5. Handle the case where `parentPostId` is set: show a "Replying to @handle" header and a link to the parent post.

### Update `PostCard.razor`

- Add `likeCount`, `likedByMe`, `replyCount`, `repostCount`, `isRepostedByMe` to the props.
- Add action buttons (like, reply, repost, delete) with correct event callbacks.
- When `PostDto.originalPostId` is set, show a "Reposted by @{authorHandle}" label above the embedded original post.
- When the post has `media`, render a `PostMediaGrid.razor` component below the text.

### New Component: `PostMediaGrid.razor`

- Displays 1–4 images in a responsive grid (single image: full width; 2 images: side-by-side; 3–4: 2×2 grid).
- For video, shows a `<video controls>` element.
- Images link to full-size via `GET /api/post-media/{assetId}`.

### New Component: `PostContent.razor`

- Renders post text with `@mention` and `#hashtag` segments styled distinctly (e.g. blue links).
- Mentions link to `/profile/{handle}`.
- Hashtags link to `/posts/search?q=%23{tag}`.

### API Client Updates

Add methods:
- `LikePostAsync(postId)`
- `UnlikePostAsync(postId)`
- `CreateReplyAsync(parentPostId, content)`
- `CreateRepostAsync(originalPostId, commentary?)`
- `DeleteRepostAsync(originalPostId)`
- `GetPostWithConversationAsync(postId)`

## Acceptance Criteria

- Clicking the like button on a feed post toggles the like; count updates immediately (optimistic UI).
- Clicking the reply button opens an inline composer; submitting creates the reply and shows it in the feed.
- Clicking repost shows options; selecting "Repost" or "Quote" creates the repost.
- "Undo repost" is visible and works if the user has already reposted.
- `/posts/{id}` shows the post with nested replies up to 3 levels.
- Replies in the conversation tree also have like/reply/repost buttons.
- Post media (images) renders in a responsive grid.
- `@mentions` and `#hashtags` in post content are styled as links.
