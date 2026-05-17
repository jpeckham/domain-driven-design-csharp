# Prompt 16 — Frontend: Post Media Upload

## Context

After prompt 11, the backend supports attaching images and video to posts. This prompt adds the media upload UI to the post creation composer in `Feed.razor` and the reply composer.

## Task

### Update Post Composer in `Feed.razor`

The post creation area (textarea + "Post" button) should gain a media attachment feature:

1. Add a **media attach button** (paperclip or image icon) next to the "Post" button.
2. Clicking it opens an `<InputFile>` multiple file selector.
3. The user can select up to 4 images OR 1 video (validate on selection):
   - Reject if mixing image and video types.
   - Reject if more than 4 files selected.
   - Show an inline error for invalid selections.
4. Show a **media preview strip** below the textarea:
   - Each selected file shows a thumbnail (use `URL.createObjectURL` via JS interop, or rely on Blazor's `IBrowserFile` preview).
   - Each thumbnail has an "×" remove button.
   - For video, show a video thumbnail or a film-strip icon placeholder.
5. On each file added, immediately start the upload in the background:
   - Call `POST /api/posts/media/upload-sessions`.
   - PUT the bytes to the returned `uploadUrl`.
   - Call `POST /api/posts/media/{assetId}/complete`.
   - Store the returned `assetId` in the component state (keyed to the file).
   - Show a spinner or progress bar per file while uploading.
6. When the user submits the post:
   - Wait for all pending uploads to complete (if still in progress, show a "Uploading…" state and block submission).
   - Pass the collected `assetIds` in the `POST /api/posts` request body.
   - On success, clear the composer and preview strip.

### New Component: `MediaUploadPreview.razor`

- Wraps a single file's upload state: file thumbnail, upload progress (0–100%), remove button.
- Emits `OnRemove` event when the × is clicked (which cancels the upload if in progress and removes it from the list).

### JS Interop (if needed)

- Create a small `mediaUtils.js` with a helper to read `File` bytes as a `Uint8Array` for the PUT request (Blazor's `OpenReadStream` may suffice — use that if possible).
- Optionally create a blob URL helper for image thumbnails.

### Reuse in Reply Composer

The same media attachment UX should be available in the inline reply composer created in prompt 15. Extract the media upload strip into a reusable `MediaAttachmentStrip.razor` component used by both composers.

### Validation Rules (client-side mirrors server-side)

- Max 4 files.
- Only `image/jpeg`, `image/png`, `image/webp`, `image/gif`, `video/mp4`.
- No mixing images and video.
- Max file size suggestion: 20 MB per file (warn the user, don't hard block — the server enforces its own limits).

### API Client Updates

Add methods (if not already present from prompt 11):
- `BeginPostMediaUploadAsync(contentType) → { assetId, uploadUrl }`
- `PutPostMediaBytesAsync(uploadUrl, bytes, contentType)`
- `CompletePostMediaUploadAsync(assetId, contentType, byteLength, width?, height?, durationMs?, altText?)`

## Acceptance Criteria

- Selecting 1–4 images and posting attaches them to the created post; they appear in the feed via `PostMediaGrid`.
- Selecting a video and posting attaches it; it plays inline in the feed.
- Selecting mixed types shows an error; selecting more than 4 shows an error.
- Files upload in parallel in the background; spinner is shown per file.
- Submission is blocked until all uploads complete.
- Removing a file from the preview cancels its upload.
- Reply composer also supports media attachment.
