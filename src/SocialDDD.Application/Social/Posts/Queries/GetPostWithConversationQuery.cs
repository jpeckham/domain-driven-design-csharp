using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Application.Social.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Social.Posts;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Application.Social.Posts.Queries;

public sealed record GetPostWithConversationQuery(Guid PostId, int DepthLimit = 3, int RepliesPerLevel = 100);

public sealed class GetPostWithConversationQueryHandler(
    IPostRepository postRepository,
    IUserRepository userRepository)
{
    public async Task<PostConversationDto> HandleAsync(
        GetPostWithConversationQuery query,
        string? requesterHandle,
        Guid? requesterUserId,
        CancellationToken ct = default)
    {
        var postId = PostId.From(query.PostId);

        var rootPost = await postRepository.GetByIdAsync(postId, ct)
            ?? throw new DomainException($"Post {query.PostId} not found.");

        var descendants = await postRepository.GetConversationAsync(
            postId, query.DepthLimit, query.RepliesPerLevel, ct);

        var allPosts = new List<Post> { rootPost }.Concat(descendants).ToList();
        Handle? requester = requesterHandle is not null ? new Handle(requesterHandle) : null;
        UserId? requesterUserIdTyped = requesterUserId.HasValue ? UserId.From(requesterUserId.Value) : null;

        async Task<PostDto> ToDtoAsync(Post p)
        {
            var author = await userRepository.GetByIdAsync(p.AuthorId, ct);
            bool likedByMe = requester is not null
                && await postRepository.IsLikedByAsync(p.Id, requester, ct);
            int repostCount = await postRepository.GetRepostCountAsync(p.Id, ct);
            bool isRepostedByMe = requesterUserIdTyped is not null
                && await postRepository.FindRepostAsync(p.Id, requesterUserIdTyped, ct) is not null;

            PostDto? originalPost = null;
            if (p.OriginalPostId is not null)
            {
                var orig = await postRepository.GetByIdAsync(p.OriginalPostId, ct);
                if (orig is not null)
                {
                    bool origLikedByMe = requester is not null
                        && await postRepository.IsLikedByAsync(orig.Id, requester, ct);
                    int origRepostCount = await postRepository.GetRepostCountAsync(orig.Id, ct);
                    var originalAuthor = await userRepository.GetByIdAsync(orig.AuthorId, ct);
                    originalPost = new PostDto(
                        orig.Id.Value,
                        orig.AuthorId.Value,
                        orig.Content?.Value,
                        orig.PostedAt,
                        orig.LikeCount,
                        origLikedByMe,
                        orig.ParentPostId?.Value,
                        orig.ReplyCount,
                        orig.Mentions.Select(h => h.Value).ToList(),
                        orig.Hashtags.ToList(),
                        orig.OriginalPostId?.Value,
                        origRepostCount,
                        false,
                        null,
                        ToMediaDtos(orig),
                        originalAuthor?.DisplayName.Value ?? "Unknown",
                        originalAuthor?.Handle.Display ?? "@unknown",
                        ProfileImageUrl(originalAuthor));  // OriginalPost — depth limited to 1
                }
            }

            return new PostDto(
                p.Id.Value,
                p.AuthorId.Value,
                p.Content?.Value,
                p.PostedAt,
                p.LikeCount,
                likedByMe,
                p.ParentPostId?.Value,
                p.ReplyCount,
                p.Mentions.Select(h => h.Value).ToList(),
                p.Hashtags.ToList(),
                p.OriginalPostId?.Value,
                repostCount,
                isRepostedByMe,
                originalPost,
                ToMediaDtos(p),
                author?.DisplayName.Value ?? "Unknown",
                author?.Handle.Display ?? "@unknown",
                ProfileImageUrl(author));
        }

        var conversationById = new Dictionary<Guid, PostConversationDto>();
        PostDto? rootParentPost = null;
        if (rootPost.ParentPostId is not null)
        {
            var parentPost = await postRepository.GetByIdAsync(rootPost.ParentPostId, ct);
            if (parentPost is not null)
                rootParentPost = await ToDtoAsync(parentPost);
        }

        foreach (var p in allPosts)
        {
            var parentPost = p.Id == rootPost.Id ? rootParentPost : null;
            conversationById[p.Id.Value] = new PostConversationDto(
                await ToDtoAsync(p),
                new List<PostConversationDto>(),
                parentPost);
        }

        foreach (var post in descendants)
        {
            if (post.ParentPostId is not null
                && conversationById.TryGetValue(post.ParentPostId.Value, out var parentConv))
            {
                parentConv.Replies.Add(conversationById[post.Id.Value]);
            }
        }

        return conversationById[rootPost.Id.Value];
    }

    private static List<PostMediaDto>? ToMediaDtos(Post post) =>
        post.Media.Count == 0
            ? null
            : post.Media
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
                .ToList();

    private static string? ProfileImageUrl(User? user) =>
        user?.ProfileImage is null ? null : $"/api/profile-images/{user.ProfileImage.AssetId}";
}
