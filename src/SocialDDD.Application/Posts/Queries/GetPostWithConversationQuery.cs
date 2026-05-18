using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;
using SocialDDD.Domain.Users;

namespace SocialDDD.Application.Posts.Queries;

public sealed record GetPostWithConversationQuery(Guid PostId, int DepthLimit = 3, int RepliesPerLevel = 100);

public sealed class GetPostWithConversationQueryHandler(
    IPostRepository postRepository)
{
    public async Task<PostConversationDto> HandleAsync(
        GetPostWithConversationQuery query,
        string? requesterHandle,
        CancellationToken ct = default)
    {
        var postId = PostId.From(query.PostId);

        var rootPost = await postRepository.GetByIdAsync(postId, ct)
            ?? throw new DomainException($"Post {query.PostId} not found.");

        var descendants = await postRepository.GetConversationAsync(
            postId, query.DepthLimit, query.RepliesPerLevel, ct);

        var allPosts = new List<Post> { rootPost }.Concat(descendants).ToList();
        var replyCountByParent = allPosts
            .Where(p => p.ParentPostId is not null)
            .GroupBy(p => p.ParentPostId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Handle? requester = requesterHandle is not null ? new Handle(requesterHandle) : null;

        async Task<PostDto> ToDtoAsync(Post p)
        {
            replyCountByParent.TryGetValue(p.Id.Value, out var replyCount);
            bool likedByMe = requester is not null
                && await postRepository.IsLikedByAsync(p.Id, requester, ct);
            return new PostDto(
                p.Id.Value,
                p.AuthorId.Value,
                p.Content.Value,
                p.PostedAt,
                p.LikeCount,
                likedByMe,
                p.ParentPostId?.Value,
                replyCount,
                p.Mentions.Select(h => h.Value).ToList(),
                p.Hashtags.ToList());
        }

        var conversationById = new Dictionary<Guid, PostConversationDto>();
        foreach (var p in allPosts)
            conversationById[p.Id.Value] = new PostConversationDto(await ToDtoAsync(p), new List<PostConversationDto>());

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
}
