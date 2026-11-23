using Ballpass.Web.Data;
using Ballpass.Web.Features.Feed;

namespace Ballpass.Web.Features.Engage;

/// <summary>Section "Engage" of appsettings.json.</summary>
public class EngageOptions
{
    public int CommentCooldownSeconds { get; set; } = 20;
    /// <summary>Comments shown under a post before "see all".</summary>
    public int CollapsedComments { get; set; } = 2;
    public int TrendWindowDays { get; set; } = 7;
    public int TrendCount { get; set; } = 6;
    /// <summary>First visit (no cookie): posts of the last N hours count as new.</summary>
    public int NewPostsDefaultHours { get; set; } = 24;
}

public static class EngageLimits
{
    public const int CommentMax = 280;
    public const string LastVisitCookie = "bp_lastvisit";
}

public enum CommentOutcome { Posted, Forbidden, Minor, RateLimited, Invalid, TooLong, NotFound }

public record PersonView(string Key, string NameEl, string NameLatin, string? Slug);

public record CommentView(int Id, string AuthorKey, PersonView Author, string Text, DateTimeOffset CreatedAt, bool IsOwn, bool CanHide, string? TextEn = null);

public record TagView(string Tag, string Label);

public record TrendingTag(string Tag, string Label, int Count);

public record Suggestion(PersonView Person, string RoleKey, string? Region, bool LookingForTeam, int EntityId = 0, string? ClubName = null, bool IsCoach = false);

/// <summary>Everything the engagement bar and the comment section of one post need.</summary>
public record PostEngagement(
    int PostId,
    int KudosCount,
    int CommentCount,
    bool LikedByMe,
    /// <summary>Most recent likers other than the viewer, newest first (at most 2).</summary>
    IReadOnlyList<PersonView> Likers,
    /// <summary>Likers other than the viewer.</summary>
    int OtherLikers,
    /// <summary>Oldest first (newest last). The 2 latest unless the post is expanded.</summary>
    IReadOnlyList<CommentView> Comments,
    bool Expanded,
    IReadOnlyList<TagView> Tags)
{
    public static PostEngagement Empty(int postId) => new(postId, 0, 0, false, [], 0, [], false, []);
}
