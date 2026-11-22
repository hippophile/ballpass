using Ballpass.Web.Features.Feed;

namespace Ballpass.Web.Features.Engage;

/// <summary>
/// Per-request cache for the engage components. Every load runs in its own DI scope (own DbContext): components of the same page initialise
/// concurrently, and the page itself holds the request-scoped context. A failing service never reaches the page: the answer is "nothing to show".
/// </summary>
public class EngageCache(IServiceScopeFactory scopes, IHttpContextAccessor http, ILogger<EngageCache> logger)
{
    private Task<IReadOnlyDictionary<int, PostEngagement>>? posts;
    private Task<IReadOnlyList<TrendingTag>>? trending;
    private Task<IReadOnlyList<Suggestion>>? suggestions;

    /// <summary>Post whose whole comment list is shown: <c>?comments={postId}</c>.</summary>
    public int? ExpandedPostId => int.TryParse(http.HttpContext?.Request.Query["comments"].ToString(), out var id) ? id : null;

    /// <summary>Loads the engagement of all posts of the page in one go (called by the page before its cards render).</summary>
    public Task PreloadAsync(FeedViewer viewer, IEnumerable<int> postIds)
    {
        var ids = postIds.ToList();
        return posts = Run(s => s.GetEngagementAsync(viewer, ids, ExpandedPostId), (IReadOnlyDictionary<int, PostEngagement>)new Dictionary<int, PostEngagement>());
    }

    public async Task<PostEngagement?> GetAsync(FeedViewer viewer, int postId)
    {
        posts ??= Run(s => s.GetEngagementAsync(viewer, [postId], ExpandedPostId), (IReadOnlyDictionary<int, PostEngagement>)new Dictionary<int, PostEngagement>());
        var all = await posts;
        return all.TryGetValue(postId, out var e) ? e : null;
    }

    public Task<IReadOnlyList<TrendingTag>> TrendingAsync() =>
        trending ??= Run(s => s.TrendingAsync(), (IReadOnlyList<TrendingTag>)[]);

    public Task<IReadOnlyList<Suggestion>> SuggestionsAsync(FeedViewer viewer) =>
        suggestions ??= Run(s => s.SuggestionsAsync(viewer, 5), (IReadOnlyList<Suggestion>)[]);

    public async Task<int> NewPostsAsync(FeedViewer viewer, DateTimeOffset since) =>
        await Run(s => s.NewPostsCountAsync(viewer, since), 0);

    private async Task<T> Run<T>(Func<IEngageService, Task<T>> call, T fallback)
    {
        try
        {
            using var scope = scopes.CreateScope();
            return await call(scope.ServiceProvider.GetRequiredService<IEngageService>());
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Engage failed; rendering without it");
            return fallback;
        }
    }
}
