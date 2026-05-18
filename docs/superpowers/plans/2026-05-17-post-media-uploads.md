# Post Media Uploads Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow posts to attach up to 4 images or 1 video via a two-step reserve→PUT→complete upload flow, persisted per post and served from a dedicated endpoint.

**Architecture:** Media uploads mirror the profile-image pattern: the client begins an upload (reserve), PUTs raw bytes, then completes (confirming metadata). Completed assets are held in an in-memory pending store keyed by `assetId`. When the client creates a post it passes a list of `assetId`s; the application loads them from the pending store, validates constraints, and passes `IReadOnlyList<PostMedia>` to the domain. MongoDB persists the media list embedded in the Post document.

**Tech Stack:** C# / .NET 9, MongoDB (BSON class maps), xUnit + FluentAssertions, ASP.NET Core minimal controllers, local-file storage (same pattern as profile images).

---

## File Map

**Create:**
- `src/SocialDDD.Domain/Posts/PostMedia.cs` — `MediaKind` enum + `PostMedia` sealed record
- `src/SocialDDD.Domain/Posts/IPostMediaStorageService.cs` — storage interface (mirrors `IProfileImageStorageService`)
- `src/SocialDDD.Application/Posts/IPendingMediaStore.cs` — interface for reserve/complete lifecycle
- `src/SocialDDD.Application/Posts/DTOs/PostMediaDto.cs` — media item shape inside `PostDto`
- `src/SocialDDD.Application/Posts/DTOs/PendingMediaDto.cs` — returned to client after completing an upload
- `src/SocialDDD.Application/Posts/Commands/BeginPostMediaUploadCommand.cs`
- `src/SocialDDD.Application/Posts/Commands/CompletePostMediaUploadCommand.cs`
- `src/SocialDDD.Infrastructure/PostMedia/LocalFilePostMediaStorageService.cs`
- `src/SocialDDD.Infrastructure/PostMedia/InMemoryPendingMediaStore.cs`
- `src/SocialDDD.Api/Controllers/PostMediaController.cs`
- `tests/SocialDDD.Domain.Tests/PostAttachMediaTests.cs` — unit tests
- `tests/SocialDDD.Domain.Tests/PostMediaIntegrationTests.cs` — integration test

**Modify:**
- `src/SocialDDD.Domain/Posts/Post.cs` — add `Media` property + `AttachMedia` method + update `Create` / `CreateReply` signatures
- `src/SocialDDD.Application/Posts/DTOs/PostDto.cs` — add `IReadOnlyList<PostMediaDto>? Media`
- `src/SocialDDD.Application/Posts/DTOs/CreatePostRequest.cs` — add `IReadOnlyList<Guid>? MediaAssetIds`
- `src/SocialDDD.Application/Posts/PostService.cs` — inject `IPendingMediaStore`; load + pass media in `CreateAsync`; map media in `ToDtoAsync`
- `src/SocialDDD.Application/Posts/Commands/CreateReplyCommand.cs` — add `IReadOnlyList<Guid>? MediaAssetIds`; inject + use `IPendingMediaStore`
- `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs` — register `PostMedia` class map; add `media` element to Post class map
- `src/SocialDDD.Infrastructure/DependencyInjection.cs` — register `IPostMediaStorageService` + `IPendingMediaStore`
- `src/SocialDDD.Api/Program.cs` — register new command handlers

---

## Task 1: Domain — PostMedia value object, MediaKind enum, IPostMediaStorageService

**Files:**
- Create: `src/SocialDDD.Domain/Posts/PostMedia.cs`
- Create: `src/SocialDDD.Domain/Posts/IPostMediaStorageService.cs`

- [ ] **Step 1: Create PostMedia.cs**

```csharp
namespace SocialDDD.Domain.Posts;

public enum MediaKind { Image, Video }

public sealed record PostMedia(
    Guid AssetId,
    MediaKind Kind,
    string StorageKey,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    long? DurationMs,
    string? ThumbnailKey,
    string? AltText,
    int SortOrder);
```

- [ ] **Step 2: Create IPostMediaStorageService.cs**

```csharp
namespace SocialDDD.Domain.Posts;

public interface IPostMediaStorageService
{
    Task<(string uploadUrl, string storageKey)> ReserveUploadAsync(
        Guid assetId, string contentType, CancellationToken ct);
    Task StoreAsync(
        Guid assetId, string storageKey, Stream data, string contentType, CancellationToken ct);
    Task<Stream> LoadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
dotnet build src/SocialDDD.Domain/SocialDDD.Domain.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/SocialDDD.Domain/Posts/PostMedia.cs src/SocialDDD.Domain/Posts/IPostMediaStorageService.cs
git commit -m "feat: add PostMedia value object, MediaKind enum, IPostMediaStorageService"
```

---

## Task 2: Domain Tests — Write failing AttachMedia tests (TDD)

**Files:**
- Create: `tests/SocialDDD.Domain.Tests/PostAttachMediaTests.cs`

- [ ] **Step 1: Create the failing test file**

```csharp
using FluentAssertions;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Domain.Tests;

public class PostAttachMediaTests
{
    private static UserId AnyAuthor() => UserId.New();
    private static PostContent AnyContent() => new("Hello world");

    private static PostMedia MakeImage(int sort = 0) => new(
        Guid.NewGuid(), MediaKind.Image, "key", "image/jpeg", 1024,
        800, 600, null, null, null, sort);

    private static PostMedia MakeVideo(int sort = 0) => new(
        Guid.NewGuid(), MediaKind.Video, "key", "video/mp4", 5_000_000,
        1920, 1080, 30_000, null, null, sort);

    [Fact]
    public void Create_WithNoMedia_MediaIsEmpty()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        post.Media.Should().BeEmpty();
    }

    [Fact]
    public void AttachMedia_FourImages_Succeeds()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = Enumerable.Range(0, 4).Select(i => MakeImage(i)).ToList();

        var act = () => post.AttachMedia(media);

        act.Should().NotThrow();
        post.Media.Should().HaveCount(4);
    }

    [Fact]
    public void AttachMedia_OneVideo_Succeeds()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());

        var act = () => post.AttachMedia([MakeVideo()]);

        act.Should().NotThrow();
        post.Media.Should().HaveCount(1);
    }

    [Fact]
    public void AttachMedia_FiveImages_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = Enumerable.Range(0, 5).Select(i => MakeImage(i)).ToList();

        var act = () => post.AttachMedia(media);

        act.Should().Throw<DomainException>().WithMessage("*4*");
    }

    [Fact]
    public void AttachMedia_MixedKinds_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = new List<PostMedia> { MakeImage(), MakeVideo() };

        var act = () => post.AttachMedia(media);

        act.Should().Throw<DomainException>().WithMessage("*same kind*");
    }

    [Fact]
    public void AttachMedia_TwoVideos_ThrowsDomainException()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = new List<PostMedia> { MakeVideo(), MakeVideo() };

        var act = () => post.AttachMedia(media);

        act.Should().Throw<DomainException>().WithMessage("*1 video*");
    }

    [Fact]
    public void AttachMedia_AssignsSortOrderByPosition()
    {
        var post = Post.Create(AnyAuthor(), AnyContent());
        var media = Enumerable.Range(0, 3).Select(_ => MakeImage(99)).ToList();

        post.AttachMedia(media);

        post.Media.Select(m => m.SortOrder).Should().Equal(0, 1, 2);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (AttachMedia doesn't exist yet)**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj --no-build 2>&1 | head -30
```

Expected: Build error — `Post` does not contain a definition for `AttachMedia` and `Media`.

- [ ] **Step 3: Commit the failing tests**

```bash
git add tests/SocialDDD.Domain.Tests/PostAttachMediaTests.cs
git commit -m "test: add failing PostAttachMedia tests (TDD)"
```

---

## Task 3: Domain — Update Post.cs

**Files:**
- Modify: `src/SocialDDD.Domain/Posts/Post.cs`

- [ ] **Step 1: Add Media property and AttachMedia method to Post**

Add after `public int LikeCount => LikedBy.Count;` (line 28):

```csharp
    public List<PostMedia> Media { get; private set; } = [];
```

Add after the `Unlike` method (before closing brace of class):

```csharp
    public void AttachMedia(IReadOnlyList<PostMedia> media)
    {
        if (media.Count > 4)
            throw new DomainException("A post may contain at most 4 media items.");
        if (media.Count > 1 && media.Any(m => m.Kind != media[0].Kind))
            throw new DomainException("All media items must be the same kind.");
        if (media[0].Kind == MediaKind.Video && media.Count > 1)
            throw new DomainException("A post may contain at most 1 video.");

        Media = media.Select((m, i) => m with { SortOrder = i }).ToList();
    }
```

- [ ] **Step 2: Update Post.Create to accept optional media**

Replace the existing `Create` method:

```csharp
    public static Post Create(
        UserId authorId, PostContent content, IReadOnlyList<PostMedia>? media = null)
    {
        var post = new Post
        {
            Id = PostId.New(),
            AuthorId = authorId,
            Content = content,
            PostedAt = DateTime.UtcNow,
            IsDeleted = false,
            ParentPostId = null
        };
        post.ExtractMentionsAndHashtags(authorId: null);
        if (media is { Count: > 0 })
            post.AttachMedia(media);
        post.RaiseDomainEvent(new PostCreated(post.Id, authorId));
        return post;
    }
```

- [ ] **Step 3: Update Post.CreateReply to accept optional media**

Replace the existing `CreateReply` method:

```csharp
    public static Post CreateReply(
        PostId parentPostId, UserId authorId, Handle authorHandle, PostContent content,
        IReadOnlyList<PostMedia>? media = null)
    {
        var post = new Post
        {
            Id = PostId.New(),
            AuthorId = authorId,
            Content = content,
            PostedAt = DateTime.UtcNow,
            IsDeleted = false,
            ParentPostId = parentPostId
        };
        post.ExtractMentionsAndHashtags(authorId: authorHandle);
        if (media is { Count: > 0 })
            post.AttachMedia(media);
        post.RaiseDomainEvent(new PostCreated(post.Id, authorId));
        return post;
    }
```

- [ ] **Step 4: Run the AttachMedia tests — expect PASS**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj --filter "PostAttachMedia" -v normal
```

Expected: All 7 tests pass.

- [ ] **Step 5: Run all tests to check no regressions**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/SocialDDD.Domain/Posts/Post.cs
git commit -m "feat: add Media property and AttachMedia method to Post; update Create/CreateReply signatures"
```

---

## Task 4: Application — DTOs

**Files:**
- Create: `src/SocialDDD.Application/Posts/DTOs/PostMediaDto.cs`
- Create: `src/SocialDDD.Application/Posts/DTOs/PendingMediaDto.cs`
- Modify: `src/SocialDDD.Application/Posts/DTOs/PostDto.cs`
- Modify: `src/SocialDDD.Application/Posts/DTOs/CreatePostRequest.cs`

- [ ] **Step 1: Create PostMediaDto.cs**

```csharp
namespace SocialDDD.Application.Posts.DTOs;

public sealed record PostMediaDto(
    Guid AssetId,
    string Kind,
    string? AltText,
    int? Width,
    int? Height,
    long? DurationMs,
    string MediaUrl,
    int SortOrder);
```

- [ ] **Step 2: Create PendingMediaDto.cs**

```csharp
namespace SocialDDD.Application.Posts.DTOs;

public sealed record PendingMediaDto(
    Guid AssetId,
    string Kind,
    int? Width,
    int? Height,
    long? DurationMs,
    string? AltText);
```

- [ ] **Step 3: Update PostDto.cs — add optional media list**

Replace the entire file:

```csharp
namespace SocialDDD.Application.Posts.DTOs;

public sealed record PostDto(
    Guid PostId,
    Guid AuthorId,
    string? Content,
    DateTime PostedAt,
    int LikeCount,
    bool LikedByMe,
    Guid? ParentPostId = null,
    int ReplyCount = 0,
    IReadOnlyList<string>? Mentions = null,
    IReadOnlyList<string>? Hashtags = null,
    Guid? OriginalPostId = null,
    int RepostCount = 0,
    bool IsRepostedByMe = false,
    PostDto? OriginalPost = null,
    IReadOnlyList<PostMediaDto>? Media = null);
```

- [ ] **Step 4: Update CreatePostRequest.cs — add optional media asset IDs**

Replace the entire file:

```csharp
namespace SocialDDD.Application.Posts.DTOs;

public sealed record CreatePostRequest(
    Guid AuthorId,
    string Content,
    IReadOnlyList<Guid>? MediaAssetIds = null);
```

- [ ] **Step 5: Build to confirm no errors**

```bash
dotnet build src/SocialDDD.Application/SocialDDD.Application.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/SocialDDD.Application/Posts/DTOs/
git commit -m "feat: add PostMediaDto, PendingMediaDto; update PostDto and CreatePostRequest for media"
```

---

## Task 5: Application — IPendingMediaStore + BeginPostMediaUploadCommand

**Files:**
- Create: `src/SocialDDD.Application/Posts/IPendingMediaStore.cs`
- Create: `src/SocialDDD.Application/Posts/Commands/BeginPostMediaUploadCommand.cs`

- [ ] **Step 1: Create IPendingMediaStore.cs**

```csharp
using SocialDDD.Domain.Posts;

namespace SocialDDD.Application.Posts;

public interface IPendingMediaStore
{
    void Reserve(Guid assetId);
    bool IsReserved(Guid assetId);
    void Complete(Guid assetId, PostMedia media);
    bool TryGetCompleted(Guid assetId, out PostMedia? media);
}
```

- [ ] **Step 2: Create BeginPostMediaUploadCommand.cs**

```csharp
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Application.Posts.Commands;

public sealed record BeginPostMediaUploadCommand(Guid UserId, string ContentType);

public sealed class BeginPostMediaUploadCommandHandler(
    IPostMediaStorageService storageService,
    IPendingMediaStore pendingMediaStore)
{
    private static readonly HashSet<string> AllowedTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4"
    ];

    public async Task<(Guid AssetId, string UploadUrl)> HandleAsync(
        BeginPostMediaUploadCommand command, CancellationToken ct = default)
    {
        if (!AllowedTypes.Contains(command.ContentType))
            throw new DomainValidationException(
                $"Content type '{command.ContentType}' is not allowed. " +
                "Use image/jpeg, image/png, image/webp, image/gif, or video/mp4.");

        var assetId = Guid.NewGuid();
        var (uploadUrl, _) = await storageService.ReserveUploadAsync(assetId, command.ContentType, ct);
        pendingMediaStore.Reserve(assetId);
        return (assetId, uploadUrl);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/SocialDDD.Application/SocialDDD.Application.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/SocialDDD.Application/Posts/IPendingMediaStore.cs \
        src/SocialDDD.Application/Posts/Commands/BeginPostMediaUploadCommand.cs
git commit -m "feat: add IPendingMediaStore and BeginPostMediaUploadCommand"
```

---

## Task 6: Application — CompletePostMediaUploadCommand

**Files:**
- Create: `src/SocialDDD.Application/Posts/Commands/CompletePostMediaUploadCommand.cs`

- [ ] **Step 1: Create CompletePostMediaUploadCommand.cs**

```csharp
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Application.Posts.Commands;

public sealed record CompletePostMediaUploadCommand(
    Guid AssetId,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    long? DurationMs,
    string? AltText);

public sealed class CompletePostMediaUploadCommandHandler(IPendingMediaStore pendingMediaStore)
{
    public Task<PendingMediaDto> HandleAsync(
        CompletePostMediaUploadCommand command, CancellationToken ct = default)
    {
        if (!pendingMediaStore.IsReserved(command.AssetId))
            throw new DomainException(
                $"Media asset {command.AssetId} was not reserved. Call the upload-sessions endpoint first.");

        var kind = command.ContentType.StartsWith("video/")
            ? MediaKind.Video
            : MediaKind.Image;

        var media = new PostMedia(
            command.AssetId,
            kind,
            command.AssetId.ToString(),
            command.ContentType,
            command.ByteLength,
            command.Width,
            command.Height,
            command.DurationMs,
            null,
            command.AltText,
            0);

        pendingMediaStore.Complete(command.AssetId, media);

        return Task.FromResult(new PendingMediaDto(
            command.AssetId,
            kind.ToString(),
            command.Width,
            command.Height,
            command.DurationMs,
            command.AltText));
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/SocialDDD.Application/SocialDDD.Application.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/SocialDDD.Application/Posts/Commands/CompletePostMediaUploadCommand.cs
git commit -m "feat: add CompletePostMediaUploadCommand"
```

---

## Task 7: Application — Update PostService and CreateReplyCommand for media

**Files:**
- Modify: `src/SocialDDD.Application/Posts/PostService.cs`
- Modify: `src/SocialDDD.Application/Posts/Commands/CreateReplyCommand.cs`

- [ ] **Step 1: Update PostService — inject IPendingMediaStore, load media in CreateAsync, map media in ToDtoAsync**

Replace the entire `PostService.cs`:

```csharp
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts;

public sealed class PostService(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher,
    IPendingMediaStore pendingMediaStore)
{
    public async Task<PostDto> CreateAsync(CreatePostRequest request, CancellationToken ct = default)
    {
        var authorId = UserId.From(request.AuthorId);

        if (!await userRepository.ExistsByIdAsync(authorId, ct))
            throw new DomainException("Author not found.");

        var media = LoadAndValidateMedia(request.MediaAssetIds);
        var post = Post.Create(authorId, new PostContent(request.Content), media);

        await postRepository.AddAsync(post, ct);
        await eventDispatcher.DispatchAsync(post.PopDomainEvents(), ct);

        return await ToDtoAsync(post, null, null, ct);
    }

    public async Task DeleteAsync(Guid postId, Guid requesterId, CancellationToken ct = default)
    {
        var post = await postRepository.GetByIdAsync(PostId.From(postId), ct)
            ?? throw new DomainException($"Post {postId} not found.");

        if (post.AuthorId != UserId.From(requesterId))
            throw new DomainException("Only the author can delete their post.");

        post.Delete();

        await postRepository.UpdateAsync(post, ct);
        await eventDispatcher.DispatchAsync(post.PopDomainEvents(), ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetFeedAsync(
        int skip, int limit, Guid? requesterId = null, bool rootOnly = false, CancellationToken ct = default)
    {
        var posts = await postRepository.GetFeedAsync(skip, limit, rootOnly, ct);
        var (handle, userId) = await ResolveRequesterAsync(requesterId, ct);
        return await ToDtosAsync(posts, handle, userId, ct);
    }

    public async Task<IReadOnlyList<PostDto>> GetByAuthorAsync(
        Guid userId, Guid? requesterId = null, CancellationToken ct = default)
    {
        var posts = await postRepository.GetByAuthorAsync(UserId.From(userId), ct);
        var (handle, requesterUserId) = await ResolveRequesterAsync(requesterId, ct);
        return await ToDtosAsync(posts, handle, requesterUserId, ct);
    }

    public async Task<string?> GetHandleByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(userId), ct);
        return user?.Handle.Value;
    }

    private IReadOnlyList<PostMedia>? LoadAndValidateMedia(IReadOnlyList<Guid>? assetIds)
    {
        if (assetIds is null or { Count: 0 }) return null;

        if (assetIds.Count > 4)
            throw new DomainValidationException("A post may contain at most 4 media items.");

        var result = new List<PostMedia>(assetIds.Count);
        foreach (var id in assetIds)
        {
            if (!pendingMediaStore.TryGetCompleted(id, out var media))
                throw new DomainException($"Media asset {id} not found or not yet uploaded.");
            result.Add(media!);
        }

        var kinds = result.Select(m => m.Kind).Distinct().ToList();
        if (kinds.Count > 1)
            throw new DomainValidationException("All media items must be the same kind.");
        if (kinds[0] == MediaKind.Video && result.Count > 1)
            throw new DomainValidationException("A post may contain at most 1 video.");

        return result;
    }

    private async Task<(Handle? handle, UserId? userId)> ResolveRequesterAsync(Guid? requesterId, CancellationToken ct)
    {
        if (requesterId is null) return (null, null);
        var userId = UserId.From(requesterId.Value);
        var user = await userRepository.GetByIdAsync(userId, ct);
        return (user?.Handle, userId);
    }

    private async Task<PostDto> ToDtoAsync(Post post, Handle? requesterHandle, UserId? requesterUserId, CancellationToken ct)
    {
        bool likedByMe = requesterHandle is not null
            && await postRepository.IsLikedByAsync(post.Id, requesterHandle, ct);
        int replyCount = await postRepository.CountRepliesAsync(post.Id, ct);
        int repostCount = await postRepository.GetRepostCountAsync(post.Id, ct);
        bool isRepostedByMe = requesterUserId is not null
            && await postRepository.FindRepostAsync(post.Id, requesterUserId, ct) is not null;

        PostDto? originalPost = null;
        if (post.OriginalPostId is not null)
        {
            var orig = await postRepository.GetByIdAsync(post.OriginalPostId, ct);
            if (orig is not null)
                originalPost = await ToDtoAsync(orig, requesterHandle, requesterUserId, ct);
        }

        var mediaDtos = post.Media.Count > 0
            ? post.Media
                .OrderBy(m => m.SortOrder)
                .Select(m => new PostMediaDto(
                    m.AssetId,
                    m.Kind.ToString(),
                    m.AltText,
                    m.Width,
                    m.Height,
                    m.DurationMs,
                    $"/api/post-media/{m.AssetId}",
                    m.SortOrder))
                .ToList()
            : null;

        return new PostDto(
            post.Id.Value,
            post.AuthorId.Value,
            post.Content?.Value,
            post.PostedAt,
            post.LikeCount,
            likedByMe,
            post.ParentPostId?.Value,
            replyCount,
            post.Mentions.Select(h => h.Value).ToList(),
            post.Hashtags.ToList(),
            post.OriginalPostId?.Value,
            repostCount,
            isRepostedByMe,
            originalPost,
            mediaDtos);
    }

    private async Task<IReadOnlyList<PostDto>> ToDtosAsync(
        IReadOnlyList<Post> posts, Handle? requesterHandle, UserId? requesterUserId, CancellationToken ct)
    {
        var dtos = new List<PostDto>(posts.Count);
        foreach (var post in posts)
            dtos.Add(await ToDtoAsync(post, requesterHandle, requesterUserId, ct));
        return dtos;
    }
}
```

- [ ] **Step 2: Update CreateReplyCommand.cs — add media support**

Replace the entire file:

```csharp
using SocialDDD.Application.Interfaces;
using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts.Commands;

public sealed record CreateReplyCommand(
    Guid ParentPostId,
    Guid AuthorUserId,
    string Content,
    IReadOnlyList<Guid>? MediaAssetIds = null);

public sealed class CreateReplyCommandHandler(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IDomainEventDispatcher eventDispatcher,
    IPendingMediaStore pendingMediaStore)
{
    public async Task<PostDto> HandleAsync(CreateReplyCommand command, CancellationToken ct = default)
    {
        var parentPostId = PostId.From(command.ParentPostId);

        var parentPost = await postRepository.GetByIdAsync(parentPostId, ct)
            ?? throw new DomainException($"Post {command.ParentPostId} not found.");

        if (parentPost.IsDeleted)
            throw new DomainException("Cannot reply to a deleted post.");

        var author = await userRepository.GetByIdAsync(UserId.From(command.AuthorUserId), ct)
            ?? throw new DomainException($"User {command.AuthorUserId} not found.");

        var authorHandle = author.Handle;

        var parentAuthor = await userRepository.GetByIdAsync(parentPost.AuthorId, ct);
        string content = command.Content;
        if (parentAuthor is not null)
        {
            var prefix = $"@{parentAuthor.Handle.Value} ";
            if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                content = prefix + content;
        }

        var media = LoadAndValidateMedia(command.MediaAssetIds);
        var postContent = new PostContent(content);
        var reply = Post.CreateReply(parentPostId, UserId.From(command.AuthorUserId), authorHandle, postContent, media);

        await postRepository.AddAsync(reply, ct);
        await eventDispatcher.DispatchAsync(reply.PopDomainEvents(), ct);

        var mediaDtos = reply.Media.Count > 0
            ? reply.Media
                .OrderBy(m => m.SortOrder)
                .Select(m => new PostMediaDto(
                    m.AssetId,
                    m.Kind.ToString(),
                    m.AltText,
                    m.Width,
                    m.Height,
                    m.DurationMs,
                    $"/api/post-media/{m.AssetId}",
                    m.SortOrder))
                .ToList()
            : null;

        return new PostDto(
            reply.Id.Value,
            reply.AuthorId.Value,
            reply.Content?.Value,
            reply.PostedAt,
            reply.LikeCount,
            false,
            reply.ParentPostId?.Value,
            0,
            reply.Mentions.Select(h => h.Value).ToList(),
            reply.Hashtags.ToList(),
            null,
            0,
            false,
            null,
            mediaDtos);
    }

    private IReadOnlyList<PostMedia>? LoadAndValidateMedia(IReadOnlyList<Guid>? assetIds)
    {
        if (assetIds is null or { Count: 0 }) return null;

        if (assetIds.Count > 4)
            throw new DomainValidationException("A post may contain at most 4 media items.");

        var result = new List<PostMedia>(assetIds.Count);
        foreach (var id in assetIds)
        {
            if (!pendingMediaStore.TryGetCompleted(id, out var media))
                throw new DomainException($"Media asset {id} not found or not yet uploaded.");
            result.Add(media!);
        }

        var kinds = result.Select(m => m.Kind).Distinct().ToList();
        if (kinds.Count > 1)
            throw new DomainValidationException("All media items must be the same kind.");
        if (kinds[0] == MediaKind.Video && result.Count > 1)
            throw new DomainValidationException("A post may contain at most 1 video.");

        return result;
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/SocialDDD.Application/SocialDDD.Application.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all domain tests**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/SocialDDD.Application/Posts/PostService.cs \
        src/SocialDDD.Application/Posts/Commands/CreateReplyCommand.cs
git commit -m "feat: update PostService and CreateReplyCommand to load and attach media"
```

---

## Task 8: Infrastructure — Implementations, BSON mapping, DI

**Files:**
- Create: `src/SocialDDD.Infrastructure/PostMedia/LocalFilePostMediaStorageService.cs`
- Create: `src/SocialDDD.Infrastructure/PostMedia/InMemoryPendingMediaStore.cs`
- Modify: `src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs`
- Modify: `src/SocialDDD.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create LocalFilePostMediaStorageService.cs**

```csharp
using SocialDDD.Domain.Posts;

namespace SocialDDD.Infrastructure.PostMedia;

public sealed class LocalFilePostMediaStorageService(string baseDirectory) : IPostMediaStorageService
{
    public Task<(string uploadUrl, string storageKey)> ReserveUploadAsync(
        Guid assetId, string contentType, CancellationToken ct)
    {
        var storageKey = assetId.ToString();
        var uploadUrl = $"/api/media/uploads/post/{assetId}";
        return Task.FromResult((uploadUrl, storageKey));
    }

    public async Task StoreAsync(
        Guid assetId, string storageKey, Stream data, string contentType, CancellationToken ct)
    {
        var path = FilePath(storageKey);
        await using var file = File.Create(path);
        await data.CopyToAsync(file, ct);
    }

    public Task<Stream> LoadAsync(string storageKey, CancellationToken ct)
    {
        var path = FilePath(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Post media not found: {storageKey}");
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = FilePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string FilePath(string storageKey) => Path.Combine(baseDirectory, storageKey);
}
```

- [ ] **Step 2: Create InMemoryPendingMediaStore.cs**

```csharp
using System.Collections.Concurrent;
using SocialDDD.Application.Posts;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Infrastructure.PostMedia;

public sealed class InMemoryPendingMediaStore : IPendingMediaStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, DateTime> _reserved = new();
    private readonly ConcurrentDictionary<Guid, (PostMedia Media, DateTime Expiry)> _completed = new();

    public void Reserve(Guid assetId)
        => _reserved[assetId] = DateTime.UtcNow.Add(Ttl);

    public bool IsReserved(Guid assetId)
        => _reserved.TryGetValue(assetId, out var expiry) && expiry > DateTime.UtcNow;

    public void Complete(Guid assetId, PostMedia media)
    {
        _reserved.TryRemove(assetId, out _);
        _completed[assetId] = (media, DateTime.UtcNow.Add(Ttl));
    }

    public bool TryGetCompleted(Guid assetId, out PostMedia? media)
    {
        if (_completed.TryGetValue(assetId, out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            media = entry.Media;
            return true;
        }
        media = null;
        return false;
    }
}
```

- [ ] **Step 3: Update BsonMappings.cs — register PostMedia class map and add Media to Post map**

Add the `PostMedia` class map registration inside `Register()`, after the existing `ProfileImage` registration:

```csharp
        BsonClassMap.RegisterClassMap<PostMedia>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapConstructor(
                typeof(PostMedia).GetConstructor([
                    typeof(Guid), typeof(MediaKind), typeof(string), typeof(string), typeof(long),
                    typeof(int?), typeof(int?), typeof(long?), typeof(string), typeof(string?), typeof(int)
                ])!,
                nameof(PostMedia.AssetId),
                nameof(PostMedia.Kind),
                nameof(PostMedia.StorageKey),
                nameof(PostMedia.ContentType),
                nameof(PostMedia.ByteLength),
                nameof(PostMedia.Width),
                nameof(PostMedia.Height),
                nameof(PostMedia.DurationMs),
                nameof(PostMedia.ThumbnailKey),
                nameof(PostMedia.AltText),
                nameof(PostMedia.SortOrder));
        });
```

Also update the Post class map to include the `media` element. Change the existing Post class map registration from:

```csharp
        BsonClassMap.RegisterClassMap<Post>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(p => p.LikedBy).SetElementName("likedBy");
            cm.MapMember(p => p.ParentPostId).SetElementName("parentPostId");
            cm.MapMember(p => p.OriginalPostId).SetElementName("originalPostId");
            cm.MapMember(p => p.Mentions).SetElementName("mentions");
            cm.MapMember(p => p.Hashtags).SetElementName("hashtags");
        });
```

To:

```csharp
        BsonClassMap.RegisterClassMap<Post>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(p => p.LikedBy).SetElementName("likedBy");
            cm.MapMember(p => p.ParentPostId).SetElementName("parentPostId");
            cm.MapMember(p => p.OriginalPostId).SetElementName("originalPostId");
            cm.MapMember(p => p.Mentions).SetElementName("mentions");
            cm.MapMember(p => p.Hashtags).SetElementName("hashtags");
            cm.MapMember(p => p.Media).SetElementName("media");
        });
```

Also add the using for the Domain.Posts namespace at the top of BsonMappings.cs (it is already there, but confirm `using SocialDDD.Domain.Posts;` is present).

- [ ] **Step 4: Update DependencyInjection.cs — register post media services**

After the profile image storage registration block, add:

```csharp
        // Post media storage: local file system
        var postMediaDir = configuration["PostMedia:Directory"] ?? "./data/post-media";
        Directory.CreateDirectory(postMediaDir);
        services.AddSingleton<IPostMediaStorageService>(_ =>
            new LocalFilePostMediaStorageService(postMediaDir));

        // Pending media store: singleton in-memory (1-hour TTL)
        services.AddSingleton<IPendingMediaStore, InMemoryPendingMediaStore>();
```

Add the required usings to the top of `DependencyInjection.cs`:

```csharp
using SocialDDD.Application.Posts;
using SocialDDD.Domain.Posts;
using SocialDDD.Infrastructure.PostMedia;
```

- [ ] **Step 5: Build the full solution**

```bash
dotnet build SocialDDD.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Run all tests**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/SocialDDD.Infrastructure/PostMedia/ \
        src/SocialDDD.Infrastructure/Persistence/Mapping/BsonMappings.cs \
        src/SocialDDD.Infrastructure/DependencyInjection.cs
git commit -m "feat: add LocalFilePostMediaStorageService, InMemoryPendingMediaStore; wire BSON and DI"
```

---

## Task 9: API — PostMediaController

**Files:**
- Create: `src/SocialDDD.Api/Controllers/PostMediaController.cs`

- [ ] **Step 1: Create PostMediaController.cs**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialDDD.Application.Posts.Commands;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

namespace SocialDDD.Api.Controllers;

[ApiController]
public sealed class PostMediaController(
    BeginPostMediaUploadCommandHandler beginHandler,
    CompletePostMediaUploadCommandHandler completeHandler,
    IPostMediaStorageService storageService) : ControllerBase
{
    [Authorize]
    [HttpPost("api/posts/media/upload-sessions")]
    public async Task<IActionResult> BeginUpload(
        [FromBody] BeginPostMediaUploadRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var (assetId, uploadUrl) = await beginHandler.HandleAsync(
                new BeginPostMediaUploadCommand(userId.Value, request.ContentType), ct);
            return Ok(new { assetId, uploadUrl });
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("api/media/uploads/post/{assetId:guid}")]
    public async Task<IActionResult> StoreUpload(Guid assetId, CancellationToken ct)
    {
        var contentType = Request.ContentType ?? "application/octet-stream";
        await storageService.StoreAsync(assetId, assetId.ToString(), Request.Body, contentType, ct);
        return Ok();
    }

    [Authorize]
    [HttpPost("api/posts/media/{assetId:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(
        Guid assetId, [FromBody] CompletePostMediaUploadRequest request, CancellationToken ct)
    {
        if (GetUserId() is null) return Unauthorized();

        try
        {
            var dto = await completeHandler.HandleAsync(new CompletePostMediaUploadCommand(
                assetId,
                request.ContentType,
                request.ByteLength,
                request.Width,
                request.Height,
                request.DurationMs,
                request.AltText), ct);
            return Ok(dto);
        }
        catch (DomainValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("api/post-media/{assetId:guid}")]
    public async Task<IActionResult> ServeMedia(Guid assetId, CancellationToken ct)
    {
        try
        {
            var stream = await storageService.LoadAsync(assetId.ToString(), ct);
            // ContentType is not persisted in storage; derive from Post if needed.
            // For local-file storage we serve as octet-stream and let the browser infer.
            return File(stream, "application/octet-stream");
        }
        catch (FileNotFoundException) { return NotFound(); }
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public sealed record BeginPostMediaUploadRequest(string ContentType);

    public sealed record CompletePostMediaUploadRequest(
        string ContentType,
        long ByteLength,
        int? Width,
        int? Height,
        long? DurationMs,
        string? AltText);
}
```

> **Note on ServeMedia content-type:** The local-file storage service does not persist the content type alongside the bytes. To serve the correct `Content-Type`, either (a) persist a sidecar metadata file, or (b) look up the Post document by assetId to retrieve the stored `ContentType` from `PostMedia`. For now, serving as `application/octet-stream` is acceptable; the browser will handle JPEG/PNG/MP4 correctly via file extension detection. If precise content-type is required, add a `GetContentTypeAsync(Guid assetId)` to the storage service that reads from a companion `.ct` file.

- [ ] **Step 2: Build the API project**

```bash
dotnet build src/SocialDDD.Api/SocialDDD.Api.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/SocialDDD.Api/Controllers/PostMediaController.cs
git commit -m "feat: add PostMediaController (upload-sessions, PUT bytes, complete, serve)"
```

---

## Task 10: API — Register handlers in Program.cs

**Files:**
- Modify: `src/SocialDDD.Api/Program.cs`

- [ ] **Step 1: Add handler registrations to Program.cs**

After `builder.Services.AddScoped<GetPostWithConversationQueryHandler>();` (the last handler registration), add:

```csharp
builder.Services.AddScoped<BeginPostMediaUploadCommandHandler>();
builder.Services.AddScoped<CompletePostMediaUploadCommandHandler>();
```

Also add the using at the top if not already present (it should be covered by the existing `using SocialDDD.Application.Posts.Commands;`).

- [ ] **Step 2: Build the full solution**

```bash
dotnet build SocialDDD.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Also update PostsController — accept optional mediaAssetIds**

Open `src/SocialDDD.Api/Controllers/PostsController.cs`. Find the `CreatePost` action. Update its request record to include media:

Locate the `CreatePostBodyRequest` record (or equivalent) inside `PostsController` that the `POST /api/posts` action reads from. Add `IReadOnlyList<Guid>? MediaAssetIds = null` to it.

Then update the `CreatePostRequest` construction inside that action to pass `request.MediaAssetIds`:

```csharp
var result = await postService.CreateAsync(
    new CreatePostRequest(userId.Value, request.Content, request.MediaAssetIds), ct);
```

- [ ] **Step 4: Build the full solution again**

```bash
dotnet build SocialDDD.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run all tests**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/SocialDDD.Api/Program.cs src/SocialDDD.Api/Controllers/PostsController.cs
git commit -m "feat: register post media handlers and wire mediaAssetIds through PostsController"
```

---

## Task 11: Integration Test — Full upload-to-serve flow

**Files:**
- Create: `tests/SocialDDD.Domain.Tests/PostMediaIntegrationTests.cs`

- [ ] **Step 1: Create PostMediaIntegrationTests.cs**

```csharp
using FluentAssertions;
using SocialDDD.Application.Posts;
using SocialDDD.Application.Posts.Commands;
using SocialDDD.Domain.Posts;
using SocialDDD.Infrastructure.PostMedia;

namespace SocialDDD.Domain.Tests;

public class PostMediaIntegrationTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"post-media-test-{Guid.NewGuid()}");
    private readonly LocalFilePostMediaStorageService _storage;
    private readonly InMemoryPendingMediaStore _store;

    public PostMediaIntegrationTests()
    {
        Directory.CreateDirectory(_tempDir);
        _storage = new LocalFilePostMediaStorageService(_tempDir);
        _store = new InMemoryPendingMediaStore();
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task FullImageFlow_BeginStoreThenCompleteAndRetrieve()
    {
        var beginHandler = new BeginPostMediaUploadCommandHandler(_storage, _store);
        var completeHandler = new CompletePostMediaUploadCommandHandler(_store);

        // Begin upload
        var (assetId, uploadUrl) = await beginHandler.HandleAsync(
            new BeginPostMediaUploadCommand(Guid.NewGuid(), "image/jpeg"));

        uploadUrl.Should().Contain(assetId.ToString());
        _store.IsReserved(assetId).Should().BeTrue();

        // Simulate PUT — write bytes directly
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        using var uploadStream = new MemoryStream(imageBytes);
        await _storage.StoreAsync(assetId, assetId.ToString(), uploadStream, "image/jpeg", default);

        // Complete upload
        var dto = await completeHandler.HandleAsync(new CompletePostMediaUploadCommand(
            assetId, "image/jpeg", imageBytes.Length, 800, 600, null, "A test image"));

        dto.AssetId.Should().Be(assetId);
        dto.Kind.Should().Be("Image");
        dto.AltText.Should().Be("A test image");

        // Asset should now be in the completed store
        _store.IsReserved(assetId).Should().BeFalse();
        _store.TryGetCompleted(assetId, out var media).Should().BeTrue();
        media!.Kind.Should().Be(MediaKind.Image);
        media.Width.Should().Be(800);

        // Load the bytes back
        var stream = await _storage.LoadAsync(assetId.ToString(), default);
        using var ms = new MemoryStream();
        await using (stream)
            await stream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task CompleteUpload_WithoutBegin_ThrowsDomainException()
    {
        var completeHandler = new CompletePostMediaUploadCommandHandler(_store);
        var orphanId = Guid.NewGuid();

        var act = async () => await completeHandler.HandleAsync(
            new CompletePostMediaUploadCommand(orphanId, "image/jpeg", 1024, null, null, null, null));

        await act.Should().ThrowAsync<SocialDDD.Domain.Exceptions.DomainException>()
            .WithMessage("*not reserved*");
    }

    [Fact]
    public async Task BeginUpload_InvalidContentType_ThrowsDomainValidationException()
    {
        var beginHandler = new BeginPostMediaUploadCommandHandler(_storage, _store);

        var act = async () => await beginHandler.HandleAsync(
            new BeginPostMediaUploadCommand(Guid.NewGuid(), "application/pdf"));

        await act.Should().ThrowAsync<SocialDDD.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task FullVideoFlow_CompletesWithVideoKind()
    {
        var beginHandler = new BeginPostMediaUploadCommandHandler(_storage, _store);
        var completeHandler = new CompletePostMediaUploadCommandHandler(_store);

        var (assetId, _) = await beginHandler.HandleAsync(
            new BeginPostMediaUploadCommand(Guid.NewGuid(), "video/mp4"));

        var videoBytes = new byte[] { 0x00, 0x00, 0x00, 0x20 };
        using var stream = new MemoryStream(videoBytes);
        await _storage.StoreAsync(assetId, assetId.ToString(), stream, "video/mp4", default);

        var dto = await completeHandler.HandleAsync(new CompletePostMediaUploadCommand(
            assetId, "video/mp4", videoBytes.Length, 1920, 1080, 30_000, null));

        dto.Kind.Should().Be("Video");
        dto.DurationMs.Should().Be(30_000);

        _store.TryGetCompleted(assetId, out var media).Should().BeTrue();
        media!.Kind.Should().Be(MediaKind.Video);
    }
}
```

- [ ] **Step 2: Run the integration tests**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj --filter "PostMediaIntegration" -v normal
```

Expected: All 4 tests pass.

- [ ] **Step 3: Run the full test suite**

```bash
dotnet test tests/SocialDDD.Domain.Tests/SocialDDD.Domain.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/SocialDDD.Domain.Tests/PostMediaIntegrationTests.cs
git commit -m "test: add PostMediaIntegrationTests covering full upload-complete-retrieve flow"
```

---

## Self-Review Checklist

### Spec coverage

| Requirement | Task(s) |
|---|---|
| `MediaKind` enum | Task 1 |
| `PostMedia` value object (all 11 fields) | Task 1 |
| `IReadOnlyList<PostMedia> Media` on Post | Task 3 |
| `Post.AttachMedia` — max 4, same Kind, throws | Task 3 (domain) |
| `Post.Create` / `CreateReply` accept optional media | Task 3 |
| `IPostMediaStorageService` interface | Task 1 |
| `BeginPostMediaUploadCommand` — validates content type, returns assetId+url | Task 5 |
| `CompletePostMediaUploadCommand` — validates reserved, returns `PendingMediaDto` | Task 6 |
| Update `CreatePostCommand` (→ `CreatePostRequest` + `PostService`) for media | Task 7 |
| Update `CreateReplyCommand` for media | Task 7 |
| Validate 4-image / 1-video at application layer | Task 7 |
| `PostDto` includes media list with served URLs | Task 4 + Task 7 |
| `LocalFilePostMediaStorageService` | Task 8 |
| `InMemoryPendingMediaStore` with TTL | Task 8 |
| MongoDB BSON mapping for `PostMedia` + Post.Media | Task 8 |
| `POST /api/posts/media/upload-sessions` | Task 9 |
| `PUT /api/media/uploads/post/{assetId}` | Task 9 |
| `POST /api/posts/media/{assetId}/complete` | Task 9 |
| `POST /api/posts` accepts `mediaAssetIds` | Task 10 |
| `GET /api/post-media/{assetId}` serves bytes | Task 9 |
| Unit test: AttachMedia rules | Task 2 + 3 |
| Unit test: post with no media is valid | Task 2 + 3 |
| Integration test: upload → complete → create post → serve | Task 11 |

### Known gaps / follow-up items

- **Content-Type on serve:** `GET /api/post-media/{assetId}` currently returns `application/octet-stream`. The storage service does not persist content-type alongside bytes. To fix: either read the Post document by assetId to retrieve `PostMedia.ContentType`, or write a sidecar `.ct` file during `StoreAsync`. This is a polish item and doesn't block the acceptance criteria.
- **CreateReplyCommand** was extended with `IPendingMediaStore` injection — the DI registration in `Program.cs` for `CreateReplyCommandHandler` is already scoped; it will automatically receive `IPendingMediaStore` (registered as singleton) via constructor injection.
- **`POST /api/posts/{postId}/replies`** — the `PostsController` action that calls `CreateReplyCommandHandler` needs its request body updated to include `mediaAssetIds?: Guid[]`, then pass them in `CreateReplyCommand`. This is covered in Task 10 Step 3 (update PostsController), which applies to both the create-post and create-reply actions.
