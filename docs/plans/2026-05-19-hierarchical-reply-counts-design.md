# Hierarchical Reply Counts Design

## Goal

Reply counts should include every non-deleted reply below a post, not only direct replies. In a thread `A -> B -> C`, `A.ReplyCount` is `2` and `B.ReplyCount` is `1`.

## Architecture

Use stored aggregate reply counts rather than recursive counting during reads. Each reply stores the chain of ancestor post IDs above it, and reply creation increments the stored reply count for the direct parent and every ancestor. This keeps feed, profile, search, and post-card reads O(1) for reply counts.

## Data Flow

When creating a reply:

1. Load the parent post.
2. Build the new reply's `AncestorPostIds` from the parent's ancestors plus the parent ID.
3. Insert the reply.
4. Increment `ReplyCount` on every post in the new reply's ancestor path.
5. Return the new reply DTO with `ReplyCount = 0`.

When reading posts, DTO mapping uses the stored `Post.ReplyCount`.

## Persistence

MongoDB persists `replyCount` and `ancestorPostIds` on post documents. Existing posts without those fields default to `0` and an empty ancestor list when read.

## Testing

Add tests proving reply creation increments every ancestor in a reply chain. Keep conversation and feed DTO mapping covered by using stored counts instead of direct-child counting.
