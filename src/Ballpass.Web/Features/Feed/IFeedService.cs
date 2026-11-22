using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Feed;

public interface IFeedService
{
    Task<FeedPageResult> GetFeedAsync(FeedViewer viewer, FeedTab tab, string? before, CancellationToken ct = default);

    /// <summary>Newest public posts for the home strip (read-only), with the viewer's hide / block / report filters.</summary>
    Task<IReadOnlyList<FeedItem>> GetPreviewAsync(FeedViewer viewer, int count, CancellationToken ct = default);

    Task<PostOutcome> CreateAsync(FeedViewer viewer, NewPost input, CancellationToken ct = default);

    /// <summary>Idempotent: sets kudos to <paramref name="on"/>. False when the post is missing, hidden or the viewer's own.</summary>
    Task<bool> SetKudosAsync(FeedViewer viewer, int postId, bool on, CancellationToken ct = default);

    Task<ModOutcome> ReportAsync(FeedViewer viewer, int postId, string? reason, CancellationToken ct = default);
    Task<ModOutcome> HideAsync(FeedViewer viewer, int postId, CancellationToken ct = default);
    Task<ModOutcome> BlockAuthorAsync(FeedViewer viewer, int postId, CancellationToken ct = default);
}

public interface IFollowService
{
    /// <summary>Idempotent. False when refused (self, blocked, unknown key).</summary>
    Task<bool> FollowAsync(string followerKey, string targetKey, CancellationToken ct = default);
    Task UnfollowAsync(string followerKey, string targetKey, CancellationToken ct = default);
    Task<bool> IsFollowingAsync(string followerKey, string targetKey, CancellationToken ct = default);
}
