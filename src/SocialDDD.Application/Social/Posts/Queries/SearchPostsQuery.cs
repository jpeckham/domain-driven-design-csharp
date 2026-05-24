using SocialDDD.Domain.Social.Profiles;
using SocialDDD.Application.Social.Posts.DTOs;
using SocialDDD.Domain.Identity.Users;

namespace SocialDDD.Application.Social.Posts.Queries;

public sealed record SearchPostsQuery(
    string? Query,
    Handle? RequesterHandle = null,
    int Limit = 20,
    int Offset = 0);

public sealed class SearchPostsQueryHandler(PostService postService)
{
    public Task<SearchResultsDto> HandleAsync(SearchPostsQuery query, CancellationToken ct = default) =>
        postService.SearchAsync(query, ct);
}
