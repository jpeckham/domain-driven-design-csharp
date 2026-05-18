using SocialDDD.Domain.Posts;

namespace SocialDDD.Infrastructure.PostMediaStorage;

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
