using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Users.Queries;

public sealed record GetProfileImageQuery(Guid AssetId);

public sealed class GetProfileImageQueryHandler(
    IUserRepository userRepository,
    IProfileImageStorageService storageService)
{
    public async Task<(Stream Stream, string ContentType)> HandleAsync(
        GetProfileImageQuery query, CancellationToken ct = default)
    {
        var user = await userRepository.FindByProfileImageAssetIdAsync(query.AssetId, ct)
            ?? throw new DomainException($"Profile image {query.AssetId} not found.");

        var image = user.ProfileImage!;
        var stream = await storageService.LoadAsync(image.StorageKey, ct);
        return (stream, image.ContentType);
    }
}
