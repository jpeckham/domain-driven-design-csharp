# Hierarchical Reply Counts Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Store and return reply counts that include all descendant replies in a post hierarchy.

**Architecture:** Add `ReplyCount` and `AncestorPostIds` to the `Post` aggregate. On reply creation, copy the parent's ancestor path, append the parent ID, insert the reply, and atomically increment every ancestor's stored reply count. DTO mapping reads the stored count.

**Tech Stack:** C#/.NET, xUnit, FluentAssertions, MongoDB driver.

---

### Task 1: Failing Application Test

**Files:**
- Modify: `tests/SocialDDD.Domain.Tests/CreateReplyCommandHandlerTests.cs`

**Step 1:** Add a test that creates post `A`, reply `B`, then replies to `B` as `C`.

**Step 2:** Assert the fake repository was asked to increment both `A` and `B`, and that the inserted reply `C` stores `[A, B]` as its ancestor path.

**Step 3:** Run:

```powershell
dotnet test tests\SocialDDD.Domain.Tests\SocialDDD.Domain.Tests.csproj --filter CreateReplyCommandHandlerTests
```

Expected: fail because the aggregate and repository do not yet support hierarchical counts.

### Task 2: Domain Model

**Files:**
- Modify: `src/SocialDDD.Domain/Posts/Post.cs`

**Step 1:** Add `ReplyCount` with private setter and `AncestorPostIds` with private setter.

**Step 2:** Update `CreateReply` to accept an optional ancestor path and store it.

**Step 3:** Add `IncrementReplyCount()` for repository/test use.

### Task 3: Repository Contract and Mongo Persistence

**Files:**
- Modify: `src/SocialDDD.Domain/Posts/IPostRepository.cs`
- Modify: `src/SocialDDD.Infrastructure/Persistence/Posts/PostRepository.cs`
- Modify: `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs`
- Modify fake repositories in tests as required by the interface.

**Step 1:** Add `IncrementReplyCountsAsync(IReadOnlyList<PostId> postIds, CancellationToken ct = default)`.

**Step 2:** Implement it in MongoDB with a single `UpdateMany` using `$inc` on `replyCount`.

**Step 3:** Map `replyCount` and `ancestorPostIds`.

### Task 4: Application Mapping

**Files:**
- Modify: `src/SocialDDD.Application/Posts/Commands/CreateReplyCommand.cs`
- Modify: `src/SocialDDD.Application/Posts/PostService.cs`
- Modify: `src/SocialDDD.Application/Posts/Queries/GetPostWithConversationQuery.cs`

**Step 1:** In `CreateReplyCommandHandler`, compute the ancestor path from the parent, pass it to `Post.CreateReply`, and increment all ancestors after inserting the reply.

**Step 2:** Replace direct-count DTO mapping with stored `post.ReplyCount`.

### Task 5: Verification

Run:

```powershell
dotnet test tests\SocialDDD.Domain.Tests\SocialDDD.Domain.Tests.csproj --filter "CreateReplyCommandHandlerTests|GetPostWithConversationQueryTests|PostReplyTests"
dotnet test tests\SocialDDD.Domain.Tests\SocialDDD.Domain.Tests.csproj
```
