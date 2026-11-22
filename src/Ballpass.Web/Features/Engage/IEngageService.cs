using Ballpass.Web.Features.Feed;

namespace Ballpass.Web.Features.Engage;

public interface IEngageService
{
    /// <summary>Engagement of the given posts for this viewer. <paramref name="expandedPostId"/> gets its whole comment list.</summary>
    Task<IReadOnlyDictionary<int, PostEngagement>> GetEngagementAsync(FeedViewer viewer, IReadOnlyList<int> postIds, int? expandedPostId, CancellationToken ct = default);

    Task<CommentOutcome> AddCommentAsync(FeedViewer viewer, int postId, string? text, CancellationToken ct = default);
    Task<ModOutcome> DeleteCommentAsync(FeedViewer viewer, int commentId, CancellationToken ct = default);
    /// <summary>The post author hides a comment on their post for everyone.</summary>
    Task<ModOutcome> HideCommentAsync(FeedViewer viewer, int commentId, CancellationToken ct = default);
    Task<ModOutcome> ReportCommentAsync(FeedViewer viewer, int commentId, string? reason, CancellationToken ct = default);

    /// <summary>Top tags of the trend window with counts (non-hidden posts only).</summary>
    Task<IReadOnlyList<TrendingTag>> TrendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Suggestion>> SuggestionsAsync(FeedViewer viewer, int take, CancellationToken ct = default);
    /// <summary>Visible posts newer than <paramref name="since"/> (not the viewer's own).</summary>
    Task<int> NewPostsCountAsync(FeedViewer viewer, DateTimeOffset since, CancellationToken ct = default);
}
