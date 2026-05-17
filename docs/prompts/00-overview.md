# DDD Social App — Incremental Build Prompts

## Overview

This directory contains a sequenced set of prompts to build the `domain-driven-design-csharp` social app to full feature parity with the `clean-architecture-csharp` reference app — but implemented using Domain-Driven Design patterns.

The reference app is a Twitter/X-style microblogging platform with auth, posts, likes, replies, reposts, follows, blocks, media uploads, and a Blazor WebAssembly frontend.

## Current State (before these prompts)

The DDD project already has:
- `User` aggregate with `UserId`, `Username`, `Email`, `PasswordHash`
- `Post` aggregate with `PostId`, `AuthorId`, `Content`, `PostedAt`, `IsDeleted`
- `UserService` (register, login, get-by-id)
- `PostService` (create, delete, feed, by-author)
- MongoDB persistence for both aggregates
- JWT authentication
- Basic Blazor pages: Login, Register, Feed

## Prompt Sequence

### Backend — Domain & Features

| # | File | What it adds |
|---|------|-------------|
| 01 | `01-user-handle-displayname.md` | `Handle` + `DisplayName` value objects on `User`; handle-based identity |
| 02 | `02-email-verification-registration.md` | Pending/Active account status; email verification code flow |
| 03 | `03-device-mfa-login.md` | Device-tracking + OTP email for multi-factor login |
| 04 | `04-password-reset.md` | Request/reset password via time-limited token |
| 05 | `05-likes.md` | Like / unlike posts; `likeCount` + `likedByMe` in responses |
| 06 | `06-replies.md` | Threaded replies; `ParentPostId`; mention + hashtag extraction |
| 07 | `07-reposts.md` | Reposts with optional commentary; `OriginalPostId` |
| 08 | `08-follows.md` | Follow / unfollow; `followingOnly` feed filter |
| 09 | `09-blocks.md` | Block / unblock; blocked users excluded from feed |
| 10 | `10-profile-images.md` | Two-step profile image upload; serve via dedicated endpoint |
| 11 | `11-post-media.md` | Post media uploads (up to 4 images or 1 video) |
| 12 | `12-post-search.md` | Full-text post search with MongoDB text index |

### Frontend — Blazor WebAssembly

| # | File | What it adds |
|---|------|-------------|
| 13 | `13-frontend-auth-flows.md` | Verify registration, device OTP, forgot/reset password pages |
| 14 | `14-frontend-profile-page.md` | User profile page; follow/block UI; profile image upload |
| 15 | `15-frontend-post-interactions.md` | Like, reply, repost buttons; PostDetail page with conversation |
| 16 | `16-frontend-media-upload.md` | Media attachment in post/reply composers |
| 17 | `17-frontend-feed-enhancements.md` | Following feed, search bar, infinite scroll, block-from-feed |
| 18 | `18-frontend-navigation-layout.md` | Nav bar, route guards, session restore, toasts, loading states |

## How to Use These Prompts

Each prompt file is self-contained and describes a single increment. To execute a prompt:

1. Open Claude Code in this repository.
2. Paste (or reference) the prompt content.
3. Claude will implement the increment, run tests, and confirm it builds.
4. Commit the changes before moving to the next prompt.

Each prompt builds on the previous ones. Backend prompts (01–12) should be completed before the frontend prompts that depend on them (13–18).

## Architecture Principles to Maintain

- **Aggregates** enforce their own invariants; application services orchestrate across aggregates.
- **Value objects** are immutable and self-validating (throw `DomainException` on invalid input).
- **Domain events** are raised inside aggregates for significant state changes.
- **Repository interfaces** live in the Domain layer; implementations live in Infrastructure.
- **Application services** (handlers) should not contain business rules — those belong in aggregates or value objects.
- **No direct MongoDB driver usage in Domain or Application layers.**
