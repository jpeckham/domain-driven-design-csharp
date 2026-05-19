using System.Net.Http.Json;

namespace SocialDDD.Client.Services;

public sealed class PostApiService(HttpClient http)
{
    public async Task<List<PostDto>> GetFeedAsync(int skip = 0, int limit = 20, bool followingOnly = false)
    {
        var posts = await http.GetFromJsonAsync<List<PostDto>>($"api/posts/feed?skip={skip}&limit={limit}&followingOnly={followingOnly}");
        return posts ?? [];
    }

    public async Task<SearchResultsDto?> SearchPostsAsync(string query, int limit, int offset) =>
        await http.GetFromJsonAsync<SearchResultsDto>(
            $"api/posts/search?q={Uri.EscapeDataString(query)}&limit={limit}&offset={offset}");

    public async Task<List<PostDto>> GetByAuthorAsync(Guid userId)
    {
        var posts = await http.GetFromJsonAsync<List<PostDto>>($"api/posts/by-user/{userId}");
        return posts ?? [];
    }

    public async Task<List<PostDto>> GetPostsByUserAsync(Guid userId, int limit, int offset)
    {
        var posts = await http.GetFromJsonAsync<List<PostDto>>(
            $"api/posts/by-user/{userId}?limit={limit}&offset={offset}");
        return posts ?? [];
    }

    public async Task<bool> CreateAsync(Guid authorId, string content, IReadOnlyList<Guid>? mediaAssetIds = null)
    {
        var response = await http.PostAsJsonAsync("api/posts", new { authorId, content, mediaAssetIds });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid postId)
    {
        var response = await http.DeleteAsync($"api/posts/{postId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LikePostAsync(Guid postId) =>
        (await http.PostAsync($"api/posts/{postId}/likes", null)).IsSuccessStatusCode;

    public async Task<bool> UnlikePostAsync(Guid postId) =>
        (await http.DeleteAsync($"api/posts/{postId}/likes")).IsSuccessStatusCode;

    public async Task<PostDto?> CreateReplyAsync(Guid parentPostId, string content, IReadOnlyList<Guid>? mediaAssetIds = null)
    {
        var response = await http.PostAsJsonAsync($"api/posts/{parentPostId}/replies", new { content, mediaAssetIds });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PostDto>()
            : null;
    }

    public async Task<bool> CreateRepostAsync(Guid originalPostId, string? commentary = null) =>
        (await http.PostAsJsonAsync($"api/posts/{originalPostId}/reposts", new { commentary })).IsSuccessStatusCode;

    public async Task<bool> DeleteRepostAsync(Guid originalPostId) =>
        (await http.DeleteAsync($"api/posts/{originalPostId}/reposts/mine")).IsSuccessStatusCode;

    public async Task<PostConversationDto?> GetPostWithConversationAsync(Guid postId)
    {
        return await http.GetFromJsonAsync<PostConversationDto>($"api/posts/{postId}");
    }

    public async Task<BeginPostMediaUploadResult?> BeginPostMediaUploadAsync(string contentType)
    {
        var response = await http.PostAsJsonAsync("api/posts/media/upload-sessions", new { contentType });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BeginPostMediaUploadResult>()
            : null;
    }

    public async Task<bool> PutPostMediaBytesAsync(string uploadUrl, Stream stream, string contentType)
    {
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return (await http.PutAsync(uploadUrl.TrimStart('/'), content)).IsSuccessStatusCode;
    }

    public async Task<PendingMediaDto?> CompletePostMediaUploadAsync(
        Guid assetId,
        string contentType,
        long byteLength,
        int? width = null,
        int? height = null,
        long? durationMs = null,
        string? altText = null)
    {
        var response = await http.PostAsJsonAsync($"api/posts/media/{assetId}/complete", new
        {
            contentType,
            byteLength,
            width,
            height,
            durationMs,
            altText
        });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PendingMediaDto>()
            : null;
    }

    public sealed record PostDto(
        Guid PostId,
        Guid AuthorId,
        string? Content,
        DateTime PostedAt,
        int LikeCount,
        bool LikedByMe,
        Guid? ParentPostId = null,
        int ReplyCount = 0,
        IReadOnlyList<string>? Mentions = null,
        IReadOnlyList<string>? Hashtags = null,
        Guid? OriginalPostId = null,
        int RepostCount = 0,
        bool IsRepostedByMe = false,
        PostDto? OriginalPost = null,
        IReadOnlyList<PostMediaDto>? Media = null,
        string? AuthorDisplayName = null,
        string? AuthorHandle = null,
        string? AuthorProfileImageUrl = null);

    public sealed record PostMediaDto(
        Guid AssetId,
        string Kind,
        string AltText,
        int? Width,
        int? Height,
        long? DurationMs,
        string MediaUrl,
        int SortOrder);

    public sealed record PostConversationDto(PostDto Post, List<PostConversationDto> Replies, PostDto? ParentPost = null);
    public sealed record SearchResultsDto(List<PostDto> Posts, string Query, int Limit, int Offset);
    public sealed record BeginPostMediaUploadResult(Guid AssetId, string UploadUrl);
    public sealed record PendingMediaDto(Guid AssetId, string Kind, int? Width, int? Height, long? DurationMs, string? AltText);
}
