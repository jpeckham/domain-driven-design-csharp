# Post Deep Linking Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add post detail deep links, reply/repost context, and share URL prompting.

**Architecture:** Reuse the existing `/posts/{postId}` Blazor route and `api/posts/{postId}` conversation endpoint. Extend the focused post conversation DTO with a single parent quote for replies, keep repost originals on `PostDto.OriginalPost`, and make `PostCard` navigate/share through Blazor services while stopping action-button propagation.

**Tech Stack:** ASP.NET Core, Blazor WebAssembly, xUnit, FluentAssertions.

---

### Task 1: Card Deep Links And Share

**Files:**
- Test: `tests/SocialDDD.Domain.Tests/PostCardSourceTests.cs`
- Modify: `src/SocialDDD.Client/Components/PostCard.razor`

**Step 1: Write failing tests**

Add source tests that require `PostCard` to inject `NavigationManager` and `IJSRuntime`, navigate to `/posts/{Post.PostId}`, expose a share button, call `prompt`, and stop event propagation for post action buttons.

**Step 2: Run tests to verify failure**

Run: `dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj --filter PostCardSourceTests`

**Step 3: Implement minimal code**

Inject navigation and JS services, add an article click handler, add a share button that prompts the absolute post URL, and add `@onclick:stopPropagation` to action buttons.

**Step 4: Run tests to verify pass**

Run the same filtered test command.

### Task 2: Reply Parent Quote

**Files:**
- Test: `tests/SocialDDD.Domain.Tests/PostDetailSourceTests.cs`
- Modify: `src/SocialDDD.Application/Posts/DTOs/PostConversationDto.cs`
- Modify: `src/SocialDDD.Application/Posts/Queries/GetPostWithConversationQuery.cs`
- Modify: `src/SocialDDD.Client/Services/PostApiService.cs`
- Modify: `src/SocialDDD.Client/Pages/PostDetail.razor`

**Step 1: Write failing tests**

Add source tests requiring `ParentPost` on the conversation DTO/service record and rendering a quoted parent block when the focused post is a reply.

**Step 2: Run tests to verify failure**

Run: `dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj --filter PostDetailSourceTests`

**Step 3: Implement minimal code**

Populate `ParentPost` only for the focused post when `ParentPostId` is set. Render the parent quote above the focused `PostCard`.

**Step 4: Run tests to verify pass**

Run the same filtered test command.

### Task 3: Full Verification

**Files:**
- Verify all touched C# and Razor files.

**Step 1: Run full test suite**

Run: `dotnet test SocialDDD.sln`

**Step 2: Inspect git diff**

Run: `git diff -- src tests docs/plans/2026-05-18-post-deep-linking.md`
