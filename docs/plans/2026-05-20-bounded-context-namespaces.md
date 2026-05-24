# Bounded Context Namespaces Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Reorganize the existing projects into lower-risk Identity and Social bounded-context namespaces without splitting assemblies.

**Architecture:** Keep the current `SocialDDD.Domain`, `SocialDDD.Application`, and `SocialDDD.Infrastructure` projects. Add bounded-context namespace roots inside those projects: `Identity` for account/auth/email flows and `Social` for profile, posts, follows, blocks, and media. Shared primitives and exceptions stay at the current root.

**Tech Stack:** .NET 9, ASP.NET Core API, Blazor WebAssembly client, MongoDB infrastructure, xUnit tests.

---

### Task 1: Move Domain Namespaces

**Files:**
- Modify/move: `src/SocialDDD.Domain/Users/*`
- Modify/move: `src/SocialDDD.Domain/Posts/*`
- Modify/move: `src/SocialDDD.Domain/Follows/*`
- Modify/move: `src/SocialDDD.Domain/Blocks/*`

**Steps:**
1. Move identity account files to `Domain/Identity/Users`.
2. Move identity user events to `Domain/Identity/Users/Events`.
3. Move profile-related value objects and events to `Domain/Social/Profiles`.
4. Move posts, follows, and blocks to `Domain/Social/...`.
5. Update namespaces/usings.

### Task 2: Move Application Namespaces

**Files:**
- Modify/move: `src/SocialDDD.Application/Users/*`
- Modify/move: `src/SocialDDD.Application/Posts/*`
- Modify/move: `src/SocialDDD.Application/Follows/*`

**Steps:**
1. Move account/session/verification/password reset handlers and DTOs to `Application/Identity/Accounts`.
2. Move profile image commands/queries/profile DTOs to `Application/Social/Profiles`.
3. Move posts and follows to `Application/Social/...`.
4. Update API, infrastructure, and tests.

### Task 3: Move Infrastructure Namespaces

**Files:**
- Modify/move: `src/SocialDDD.Infrastructure/Auth/*`
- Modify/move: `src/SocialDDD.Infrastructure/Emails/*`
- Modify/move: `src/SocialDDD.Infrastructure/ProfileImages/*`
- Modify/move: `src/SocialDDD.Infrastructure/PostMediaStorage/*`
- Modify/move: `src/SocialDDD.Infrastructure/Persistence/*`
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`

**Steps:**
1. Move auth/email and identity persistence to `Infrastructure/Identity/...`.
2. Move profile/post/follow/block persistence and storage to `Infrastructure/Social/...`.
3. Leave shared `MongoDbContext`, `MongoSettings`, event dispatcher, and BSON mappings in shared infrastructure.
4. Update dependency injection registrations.

### Task 4: Verify

**Commands:**
- `dotnet build SocialDDD.sln`
- `dotnet test SocialDDD.sln`

**Expected:** Build and tests pass after namespace refactor.
