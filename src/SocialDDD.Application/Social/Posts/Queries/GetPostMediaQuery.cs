using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Social.Posts;

namespace SocialDDD.Application.Social.Posts.Queries;

public sealed record GetPostMediaQuery(Guid AssetId);

public sealed class GetPostMediaQueryHandler(
    IPostRepository postRepository,
    IPostMediaStorageService storageService)
{
    public async Task<(Stream Stream, string ContentType)> HandleAsync(
        GetPostMediaQuery query, CancellationToken ct = default)
    {
        var post = await postRepository.FindByMediaAssetIdAsync(query.AssetId, ct)
            ?? throw new DomainException($"Post media {query.AssetId} not found.");

        var media = post.Media.Single(m => m.AssetId == query.AssetId);
        var stream = await storageService.LoadAsync(media.StorageKey, ct);
        return (stream, media.ContentType);
    }
}
