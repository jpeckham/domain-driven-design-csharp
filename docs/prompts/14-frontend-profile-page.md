# Prompt 14 — Frontend: User Profile Page

## Context

After prompts 01, 08, 09, and 10, the backend supports handles, display names, profile images, follows, and blocks. This prompt adds a user profile page to the Blazor frontend.

## Task

### New Page: `UserProfile.razor` (`/profile/{handle}`)

Display a user's public profile:

**Header section:**
- Profile image (or a placeholder avatar if none set).
- Display name (large text).
- Handle (e.g. `@alice`, smaller text).
- Follower count and following count (clickable, opens a modal or navigates — keep simple for now, just display the count).
- If the logged-in user is viewing **their own** profile:
  - "Edit display name" inline input (calls `PUT /api/users/me/display-name`).
  - "Upload photo" button → opens file picker → calls the two-step profile image upload flow.
  - "Remove photo" button (only if they have a photo).
- If the logged-in user is viewing **someone else's** profile:
  - Follow / Unfollow button (calls `POST /api/users/{handle}/follows` or `DELETE /api/users/{handle}/follows`).
  - Block / Unblock button (calls `POST /api/users/{handle}/blocks` or `DELETE /api/users/{handle}/blocks`).

**Posts section:**
- List of the user's posts (most recent first), using the existing `PostCard` component (create a minimal one if it doesn't exist).
- Each `PostCard` should show: author display name + handle, post content, timestamp, like count, reply count, repost count.
- Paginate with a "Load more" button (calls `GET /api/posts/by-user/{userId}` with offset).

### New / Updated Components

1. **`PostCard.razor`** (create if it doesn't exist):
   - Props: `PostDto post`, `bool isOwner`, `EventCallback OnDelete`, `EventCallback OnLike`, `EventCallback OnReply`, `EventCallback OnRepost`.
   - Shows author info, content (with `@mention` and `#hashtag` highlighting), timestamp, action buttons.
   - If `isOwner`, shows a "Delete" button.

2. **`ProfileImageUpload.razor`** (inline component used in the profile page):
   - `<InputFile>` for selecting a file.
   - On file selected: call `POST /api/users/me/profile-image/upload-sessions` → PUT bytes → `POST /api/users/me/profile-image/complete`.
   - Show upload progress indicator.
   - On completion, refresh the profile image displayed.

3. **`AuthorThumbnail.razor`**:
   - Shows a small circular profile image (or initials placeholder) with the user's display name and handle as a link to `/profile/{handle}`.

### Navigation

- Update `MainLayout.razor` to show a link to `/profile/{currentUserHandle}` when logged in.
- Update `Feed.razor` post cards to link author names to `/profile/{handle}`.

### API Client Updates

Add methods:
- `GetUserProfileAsync(handle)`
- `FollowUserAsync(handle)`
- `UnfollowUserAsync(handle)`
- `BlockUserAsync(handle)`
- `UnblockUserAsync(handle)`
- `UpdateDisplayNameAsync(newDisplayName)`
- `BeginProfileImageUploadAsync(contentType)`
- `PutProfileImageBytesAsync(uploadUrl, bytes, contentType)`
- `CompleteProfileImageUploadAsync(assetId, contentType, byteLength, width, height)`
- `RemoveProfileImageAsync()`
- `GetPostsByUserAsync(handle, limit, offset)`

## Acceptance Criteria

- `/profile/alice` shows Alice's profile with follower count, posts, and profile image.
- Logged-in user can edit their own display name inline.
- Profile image upload flow completes and the new image is shown immediately.
- Follow/Unfollow button updates correctly after clicking.
- Block button blocks the user; their posts disappear from the logged-in user's feed.
- Posts section shows the user's posts with pagination.
