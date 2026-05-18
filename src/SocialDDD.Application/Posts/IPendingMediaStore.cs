using SocialDDD.Domain.Posts;

namespace SocialDDD.Application.Posts;

public interface IPendingMediaStore
{
    void Reserve(Guid assetId);
    bool IsReserved(Guid assetId);
    void Complete(Guid assetId, PostMedia media);
    bool TryGetCompleted(Guid assetId, out PostMedia? media);
}
