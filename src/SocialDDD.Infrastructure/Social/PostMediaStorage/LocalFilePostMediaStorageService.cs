using SocialDDD.Domain.Social.Posts;

namespace SocialDDD.Infrastructure.Social.PostMediaStorage;

public sealed class LocalFilePostMediaStorageService(string baseDirectory) : IPostMediaStorageService
{
    private readonly string _baseDirectory = EnsureDirectory(baseDirectory);

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
        await File.WriteAllTextAsync(FilePath(storageKey + ".ct"), contentType, ct);
    }

    public Task<Stream> LoadAsync(string storageKey, CancellationToken ct)
    {
        var path = FilePath(storageKey);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Post media not found: {storageKey}");
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task<string?> GetContentTypeAsync(string storageKey, CancellationToken ct)
    {
        var path = FilePath(storageKey + ".ct");
        if (!File.Exists(path)) return Task.FromResult<string?>(null);
        return File.ReadAllTextAsync(path, ct)!;
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = FilePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string FilePath(string storageKey) => Path.Combine(_baseDirectory, storageKey);

    private static string EnsureDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        return directory;
    }
}
