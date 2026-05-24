using FluentAssertions;
using SocialDDD.Application.Social.Posts;
using SocialDDD.Application.Social.Posts.Commands;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Infrastructure.Social.PostMediaStorage;

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

        await act.Should().ThrowAsync<SocialDDD.Domain.Exceptions.DomainValidationException>()
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
