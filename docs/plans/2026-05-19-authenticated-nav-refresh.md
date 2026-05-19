# Authenticated Nav Refresh Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the top navigation reflect logged-in state after login by replacing unauthenticated links with Profile and Log out.

**Architecture:** Keep auth state owned by `AuthService` and have `MainLayout` refresh its cached user display state when navigation changes. This avoids new global state plumbing while covering login, OTP verification, logout, and direct route changes.

**Tech Stack:** Blazor WebAssembly, Razor layout source tests, xUnit, FluentAssertions.

---

### Task 1: Add Layout Regression Test

**Files:**
- Create: `tests/SocialDDD.Domain.Tests/MainLayoutSourceTests.cs`
- Read: `src/SocialDDD.Client/Layout/MainLayout.razor`

**Step 1: Write the failing test**

Add tests that assert `MainLayout.razor`:
- Subscribes to `NavigationManager.LocationChanged`.
- Unsubscribes from `LocationChanged`.
- Refreshes current user state through a shared auth refresh method.
- Shows `Profile` and `Log out` when authenticated, and `Log in`/`Register` when unauthenticated.

**Step 2: Run test to verify it fails**

Run: `dotnet test tests\SocialDDD.Domain.Tests\SocialDDD.Domain.Tests.csproj --filter MainLayoutSourceTests`

Expected: fail because `MainLayout.razor` does not subscribe to route changes.

### Task 2: Refresh Layout Auth State On Navigation

**Files:**
- Modify: `src/SocialDDD.Client/Layout/MainLayout.razor`

**Step 1: Implement minimal code**

Update `MainLayout` to:
- Implement `IDisposable`.
- Move auth loading to `RefreshAuthStateAsync`.
- Subscribe to `Nav.LocationChanged` in `OnInitializedAsync`.
- In the handler, invoke `RefreshAuthStateAsync` via `InvokeAsync`.
- Unsubscribe in `Dispose`.

**Step 2: Run targeted test**

Run: `dotnet test tests\SocialDDD.Domain.Tests\SocialDDD.Domain.Tests.csproj --filter MainLayoutSourceTests`

Expected: pass.

### Task 3: Verify

**Files:**
- `tests/SocialDDD.Domain.Tests/MainLayoutSourceTests.cs`
- `src/SocialDDD.Client/Layout/MainLayout.razor`

**Step 1: Run full test project**

Run: `dotnet test tests\SocialDDD.Domain.Tests\SocialDDD.Domain.Tests.csproj`

Expected: all tests pass.

**Step 2: Inspect diff**

Run: `git diff -- src/SocialDDD.Client/Layout/MainLayout.razor tests/SocialDDD.Domain.Tests/MainLayoutSourceTests.cs`

Expected: only the layout auth refresh and its source tests changed.
