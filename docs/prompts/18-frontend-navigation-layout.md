# Prompt 18 — Frontend: Navigation, Layout, and Session Management

## Context

This prompt polishes the overall app shell: the nav bar, session management (logout), notifications, and route guards. It is the final polish pass before the app feels "complete".

## Task

### Update `MainLayout.razor`

Implement a proper navigation bar/sidebar:

**Left sidebar (desktop) / bottom bar (mobile):**
- Home (→ `/feed`)
- Search (→ `/feed?q=` or `/search`)
- Profile (→ `/profile/{currentUserHandle}`) — only when logged in
- (Optional) Notifications placeholder — not implemented yet, just a bell icon

**Top bar:**
- App logo/name ("SocialDDD")
- Right side: if logged in, show profile thumbnail + display name + dropdown menu:
  - "My Profile" → `/profile/{handle}`
  - "Log out" → clears session, navigates to `/login`
  - If not logged in, show "Log in" and "Register" buttons.

### Route Guards (`AuthorizeRouteView`)

1. Wrap the router in `MainLayout` with an `AuthorizeRouteView` or a custom redirect:
   - Pages that require auth: Feed (for post creation), Profile (for editing own profile), PostDetail (for replying).
   - If unauthenticated and navigating to an auth-required page, redirect to `/login?returnUrl={currentPath}`.
   - After login, redirect back to `returnUrl`.
2. Add `[Authorize]` attribute (or custom redirect logic) to the relevant pages.

### Session Management

1. On app startup (`App.razor` or `Program.cs`), attempt to restore the session from `localStorage`:
   - Read the stored JWT.
   - Call `GET /api/users/me` (add this endpoint if not present) to validate the token and get current user info.
   - If valid, populate `AppSession` state (current user handle, display name, profile image URL).
   - If invalid/expired, clear `localStorage`.
2. `AppSession` should be a cascading value or a scoped service that Blazor components can inject.
3. Add `GET /api/users/me` backend endpoint (authenticated) that returns the current user's profile.

### Error Handling

1. Create an `ErrorBoundary` wrapper around page content to catch unhandled exceptions and show a user-friendly "Something went wrong" message with a "Reload" button.
2. Add a toast notification component (`ToastContainer.razor`) for showing transient messages (success/error) after actions like liking, posting, following. Triggered via an injected `IToastService`.

### Loading States

- Add a consistent `LoadingSpinner.razor` component used across all pages while data is loading.
- Show it on initial page load; hide once data arrives.

### API Client Updates

- Add `GetCurrentUserAsync()` (calls `GET /api/users/me`).
- All API calls should handle 401 Unauthorized by calling `AuthService.Logout()` and redirecting to `/login`.

## Acceptance Criteria

- Navigation bar is visible on all pages with correct active state.
- Navigating to a protected page while logged out redirects to `/login`; after login, returns to the original page.
- Logging out clears the session and redirects to `/login`.
- Refreshing the page restores the session from `localStorage` if the token is still valid.
- Toast notifications appear after like/follow/post actions.
- Loading spinners appear while data is fetching.
- Unhandled errors show a friendly error message rather than a blank screen.
