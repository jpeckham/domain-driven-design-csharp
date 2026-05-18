namespace SocialDDD.Domain.Users;

public interface IProfileImageStorageService
{
    Task<(string uploadUrl, string storageKey)> ReserveUploadAsync(Guid assetId, string contentType, CancellationToken ct);
    Task StoreAsync(Guid assetId, string storageKey, Stream data, string contentType, CancellationToken ct);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct);
    Task<Stream> LoadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
