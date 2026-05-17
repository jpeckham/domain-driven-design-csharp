using System.Net.Http.Json;

namespace SocialDDD.Client.Services;

public sealed class PostApiService(HttpClient http)
{
    public async Task<List<PostDto>> GetFeedAsync(int skip = 0, int limit = 20)
    {
        var posts = await http.GetFromJsonAsync<List<PostDto>>($"api/posts/feed?skip={skip}&limit={limit}");
        return posts ?? [];
    }

    public async Task<List<PostDto>> GetByAuthorAsync(Guid userId)
    {
        var posts = await http.GetFromJsonAsync<List<PostDto>>($"api/posts/by-user/{userId}");
        return posts ?? [];
    }

    public async Task<bool> CreateAsync(Guid authorId, string content)
    {
        var response = await http.PostAsJsonAsync("api/posts", new { authorId, content });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid postId)
    {
        var response = await http.DeleteAsync($"api/posts/{postId}");
        return response.IsSuccessStatusCode;
    }

    public sealed record PostDto(Guid PostId, Guid AuthorId, string Content, DateTime PostedAt);
}
