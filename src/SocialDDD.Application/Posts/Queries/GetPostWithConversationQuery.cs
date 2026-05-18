using SocialDDD.Application.Posts.DTOs;
using SocialDDD.Domain.Exceptions;
using SocialDDD.Domain.Posts;

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

        // Count direct replies for each post (including root)
        var allPosts = new List<Post> { rootPost }.Concat(descendants).ToList();
        var replyCountByParent = allPosts
            .Where(p => p.ParentPostId is not null)
            .GroupBy(p => p.ParentPostId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Build lookup by PostId
        var postById = allPosts.ToDictionary(p => p.Id.Value);

        // Map to DTOs
        var dtoById = new Dictionary<Guid, PostConversationDto>();

        PostDto ToDto(Post p)
        {
            replyCountByParent.TryGetValue(p.Id.Value, out var replyCount);
            bool likedByMe = false; // For now, not checking per-post like status
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

        // Build tree bottom-up: iterate descendants in reverse (leaves first)
        // Actually build top-down: create nodes for all, then link
        var conversationById = allPosts.ToDictionary(
            p => p.Id.Value,
            p => new PostConversationDto(ToDto(p), new List<PostConversationDto>()));

        // Link children to parents
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
