using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Feed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ballpass.Web.Features.Engage;

public class EngageService(BallpassDbContext db, TimeProvider time, IOptions<FeedOptions> feedOptions, IOptions<EngageOptions> options) : IEngageService
{
    // ---- reading ----

    public async Task<IReadOnlyDictionary<int, PostEngagement>> GetEngagementAsync(FeedViewer viewer, IReadOnlyList<int> postIds, int? expandedPostId, CancellationToken ct = default)
    {
        var result = new Dictionary<int, PostEngagement>();
        if (postIds.Count == 0) return result;
        var ids = postIds.Distinct().ToList();
        var key = viewer.Key;
        List<string> blocked = key is null ? [] : await BlockedKeysAsync(key, ct);

        // Kudos: the count is public (everyone sees the same number); names skip the viewer and people the viewer is blocked with.
        var kudos = await db.Set<Kudos>().AsNoTracking().Where(k => ids.Contains(k.PostId))
            .Select(k => new { k.PostId, k.ActorKey, k.CreatedAt, k.Id }).ToListAsync(ct);
        var kudosByPost = kudos.ToLookup(k => k.PostId);

        // Comments visible to this viewer: not hidden, not by someone blocked either way, not reported by the viewer.
        List<int> reportedByMe = key is null ? [] : await db.Set<CommentReport>().Where(r => r.ReporterKey == key).Select(r => r.CommentId).ToListAsync(ct);
        IQueryable<Comment> Visible() => db.Set<Comment>().AsNoTracking()
            .Where(c => ids.Contains(c.PostId) && !c.IsHidden && !blocked.Contains(c.AuthorKey) && !reportedByMe.Contains(c.Id));
        var counts = await Visible().GroupBy(c => c.PostId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var collapsed = Math.Max(1, options.Value.CollapsedComments);
        var comments = new Dictionary<int, List<Comment>>();
        foreach (var id in ids.Where(counts.ContainsKey))
        {
            var q = Visible().Where(c => c.PostId == id);
            comments[id] = id == expandedPostId
                ? await q.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct)
                : (await q.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(collapsed).ToListAsync(ct)).AsEnumerable().Reverse().ToList();
        }

        var tags = (await db.Set<PostTag>().AsNoTracking().Where(t => ids.Contains(t.PostId)).OrderBy(t => t.Id).Select(t => new { t.PostId, t.Tag, t.Label }).ToListAsync(ct))
            .ToLookup(t => t.PostId);
        var postAuthors = await db.Set<Post>().AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.AuthorKey, p.PostedByCoachId }).ToDictionaryAsync(p => p.Id, ct);

        // Likers: the 2 most recent others (newest first).
        var likerKeys = new Dictionary<int, List<string>>();
        foreach (var id in ids)
            likerKeys[id] = kudosByPost[id].Where(k => k.ActorKey != key && !blocked.Contains(k.ActorKey))
                .OrderByDescending(k => k.CreatedAt).ThenByDescending(k => k.Id).Select(k => k.ActorKey).Take(2).ToList();
        var people = await PeopleAsync(likerKeys.Values.SelectMany(k => k).Concat(comments.Values.SelectMany(c => c).Select(c => c.AuthorKey)).Distinct().ToList(), ct);

        PersonView Person(string k) => people.TryGetValue(k, out var p) ? p : new PersonView(k, "—", "—", null);
        foreach (var id in ids)
        {
            var list = kudosByPost[id].ToList();
            var others = list.Count(k => k.ActorKey != key && !blocked.Contains(k.ActorKey));
            var author = postAuthors.GetValueOrDefault(id);
            var postAuthorKeys = author is null ? [] : new[] { author.AuthorKey }.Concat(author.PostedByCoachId is { } c ? [FeedKeys.Make(AuthorKind.Coach, c)] : []).ToList();
            var iAmPostAuthor = key is not null && postAuthorKeys.Contains(key);
            result[id] = new PostEngagement(id, list.Count, counts.GetValueOrDefault(id), key is not null && list.Any(k => k.ActorKey == key),
                likerKeys[id].Where(people.ContainsKey).Select(Person).ToList(), others,
                comments.GetValueOrDefault(id, []).Select(c => new CommentView(c.Id, c.AuthorKey, Person(c.AuthorKey), c.Text, c.CreatedAt,
                    key is not null && c.AuthorKey == key, iAmPostAuthor && c.AuthorKey != key)).ToList(),
                id == expandedPostId,
                tags[id].Select(t => new TagView(t.Tag, t.Label)).ToList());
        }
        return result;
    }

    private async Task<List<string>> BlockedKeysAsync(string key, CancellationToken ct) =>
        await db.Set<Block>().Where(b => b.BlockerKey == key).Select(b => b.BlockedKey)
            .Concat(db.Set<Block>().Where(b => b.BlockedKey == key).Select(b => b.BlockerKey)).ToListAsync(ct);

    /// <summary>Names (and profile slug of athletes) for actor keys. Keys of rows that no longer exist are left out.</summary>
    private async Task<Dictionary<string, PersonView>> PeopleAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var result = new Dictionary<string, PersonView>();
        var athleteIds = new List<int>();
        var coachIds = new List<int>();
        foreach (var k in keys)
        {
            if (!FeedKeys.TryParse(k, out var kind, out var id)) continue;
            if (kind == AuthorKind.Athlete) athleteIds.Add(id);
            else if (kind == AuthorKind.Coach) coachIds.Add(id);
        }
        foreach (var a in await db.Athletes.AsNoTracking().Where(a => athleteIds.Contains(a.Id)).Select(a => new { a.Id, a.FirstName, a.LastName, a.FirstNameLatin, a.LastNameLatin, a.Slug }).ToListAsync(ct))
        {
            var key = FeedKeys.Make(AuthorKind.Athlete, a.Id);
            result[key] = new PersonView(key, $"{a.FirstName} {a.LastName}", $"{a.FirstNameLatin} {a.LastNameLatin}", a.Slug);
        }
        foreach (var c in await db.Coaches.AsNoTracking().Where(c => coachIds.Contains(c.Id)).Select(c => new { c.Id, c.FirstName, c.LastName, c.FirstNameLatin, c.LastNameLatin }).ToListAsync(ct))
        {
            var key = FeedKeys.Make(AuthorKind.Coach, c.Id);
            result[key] = new PersonView(key, $"{c.FirstName} {c.LastName}", $"{c.FirstNameLatin} {c.LastNameLatin}", null);
        }
        return result;
    }

    // ---- comments ----

    public async Task<CommentOutcome> AddCommentAsync(FeedViewer viewer, int postId, string? text, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key || viewer.Role is null) return CommentOutcome.Forbidden;
        var now = FeedKeys.Truncate(time.GetUtcNow());

        var post = await db.Set<Post>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && !p.IsHidden, ct);
        if (post is null) return CommentOutcome.NotFound;
        var authorKeys = new List<string> { post.AuthorKey };
        if (post.PostedByCoachId is { } head) authorKeys.Add(FeedKeys.Make(AuthorKind.Coach, head));
        if (await db.Set<Block>().AnyAsync(b => (b.BlockerKey == key && authorKeys.Contains(b.BlockedKey)) || (authorKeys.Contains(b.BlockerKey) && b.BlockedKey == key), ct))
            return CommentOutcome.Forbidden;

        if (viewer.IsAthlete)
        {
            var athlete = await db.Athletes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == viewer.EntityId && a.IsDemo, ct);
            if (athlete is null) return CommentOutcome.Forbidden;
            if (!Safeguards.IsAdult(athlete.BirthYear, now)) return CommentOutcome.Minor; // adults only, enforced server-side
        }
        else if (!await db.Coaches.AnyAsync(c => c.Id == viewer.EntityId && c.IsDemo, ct)) return CommentOutcome.Forbidden;

        if (text is { Length: > 2000 }) return CommentOutcome.TooLong;
        var clean = Safeguards.Clean(text);
        if (clean is null) return CommentOutcome.Invalid;
        if (clean.Length > EngageLimits.CommentMax) return CommentOutcome.TooLong;

        var since = now - TimeSpan.FromSeconds(options.Value.CommentCooldownSeconds);
        if (await db.Set<Comment>().AnyAsync(c => c.AuthorKey == key && c.CreatedAt > since, ct)) return CommentOutcome.RateLimited;

        db.Set<Comment>().Add(new Comment { PostId = postId, AuthorKey = key, Text = clean, CreatedAt = now, IsDemo = true });
        await db.SaveChangesAsync(ct);
        return CommentOutcome.Posted;
    }

    public async Task<ModOutcome> DeleteCommentAsync(FeedViewer viewer, int commentId, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return ModOutcome.Forbidden;
        var comment = await db.Set<Comment>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null) return ModOutcome.NotFound;
        if (comment.AuthorKey != key) return ModOutcome.Forbidden; // the post author hides, only the comment author deletes
        await db.Set<Comment>().Where(c => c.Id == commentId).ExecuteDeleteAsync(ct);
        return ModOutcome.Ok;
    }

    public async Task<ModOutcome> HideCommentAsync(FeedViewer viewer, int commentId, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return ModOutcome.Forbidden;
        var comment = await db.Set<Comment>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null) return ModOutcome.NotFound;
        var post = await db.Set<Post>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == comment.PostId, ct);
        if (post is null) return ModOutcome.NotFound;
        var isPostAuthor = post.AuthorKey == key || (post.PostedByCoachId is { } head && FeedKeys.Make(AuthorKind.Coach, head) == key);
        if (!isPostAuthor || comment.AuthorKey == key) return ModOutcome.Forbidden;
        await db.Set<Comment>().Where(c => c.Id == commentId && !c.IsHidden)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsHidden, true).SetProperty(c => c.HiddenReason, "author"), ct);
        return ModOutcome.Ok;
    }

    public async Task<ModOutcome> ReportCommentAsync(FeedViewer viewer, int commentId, string? reason, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return ModOutcome.Forbidden;
        var comment = await db.Set<Comment>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId && !c.IsHidden, ct);
        if (comment is null) return ModOutcome.NotFound;
        if (comment.AuthorKey == key) return ModOutcome.Forbidden;
        if (reason is null || !Safeguards.ReportReasons.Contains(reason)) return ModOutcome.Invalid;

        if (!await db.Set<CommentReport>().AnyAsync(r => r.CommentId == commentId && r.ReporterKey == key, ct))
        {
            db.Add(new CommentReport { CommentId = commentId, ReporterKey = key, Reason = reason, CreatedAt = time.GetUtcNow(), IsDemo = true });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear(); // concurrent duplicate: already reported
            }
        }
        var reporters = await db.Set<CommentReport>().Where(r => r.CommentId == commentId).Select(r => r.ReporterKey).Distinct().CountAsync(ct);
        if (reporters >= feedOptions.Value.ReportHideThreshold)
            await db.Set<Comment>().Where(c => c.Id == commentId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsHidden, true).SetProperty(c => c.HiddenReason, "reports"), ct);
        return ModOutcome.Ok;
    }

    // ---- widgets ----

    public async Task<IReadOnlyList<TrendingTag>> TrendingAsync(CancellationToken ct = default)
    {
        var since = time.GetUtcNow() - TimeSpan.FromDays(options.Value.TrendWindowDays);
        var rows = await db.Set<PostTag>().AsNoTracking().Where(t => !t.Post!.IsHidden && t.Post.CreatedAt >= since)
            .Select(t => new { t.Tag, t.Label }).ToListAsync(ct);
        return rows.GroupBy(r => r.Tag)
            .Select(g => new TrendingTag(g.Key, g.GroupBy(r => r.Label).OrderByDescending(l => l.Count()).ThenByDescending(l => l.Key, StringComparer.Ordinal).First().Key, g.Count()))
            .OrderByDescending(t => t.Count).ThenBy(t => t.Tag, StringComparer.Ordinal)
            .Take(options.Value.TrendCount).ToList();
    }

    public async Task<IReadOnlyList<Suggestion>> SuggestionsAsync(FeedViewer viewer, int take, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var key = viewer.Key;
        var exclude = new HashSet<string>();
        string? myRegion = null;
        if (key is not null)
        {
            exclude.Add(key);
            exclude.UnionWith(await db.Set<Follow>().Where(f => f.FollowerKey == key).Select(f => f.TargetKey).ToListAsync(ct));
            exclude.UnionWith(await BlockedKeysAsync(key, ct));
            myRegion = viewer.IsCoach
                ? await db.Coaches.Where(c => c.Id == viewer.EntityId).Select(c => c.Region).FirstOrDefaultAsync(ct)
                : await db.Athletes.Where(a => a.Id == viewer.EntityId).Select(a => a.Region).FirstOrDefaultAsync(ct);
        }
        var activeSince = now - TimeSpan.FromDays(30);
        var active = (await db.Set<Post>().Where(p => !p.IsHidden && p.CreatedAt >= activeSince).Select(p => p.AuthorKey).Distinct().ToListAsync(ct)).ToHashSet();

        var candidates = new List<(Suggestion S, int Score, int Tie)>();
        var year = now.Year;
        var athletes = await db.Athletes.AsNoTracking().Where(a => a.IsDemo && a.BirthYear + 18 < year)
            .Select(a => new { a.Id, a.FirstName, a.LastName, a.FirstNameLatin, a.LastNameLatin, a.Slug, a.Region, a.LookingForTeam, a.Gender }).ToListAsync(ct);
        foreach (var a in athletes)
        {
            var k = FeedKeys.Make(AuthorKind.Athlete, a.Id);
            if (exclude.Contains(k)) continue;
            var score = (a.Region == myRegion ? 3 : 0) + (a.LookingForTeam ? 2 : 0) + (active.Contains(k) ? 1 : 0);
            candidates.Add((new Suggestion(new PersonView(k, $"{a.FirstName} {a.LastName}", $"{a.FirstNameLatin} {a.LastNameLatin}", a.Slug),
                a.Gender == Gender.Female ? "Role_Athlete_F" : "Role_Athlete", a.Region, a.LookingForTeam), score, Tie(a.Id, viewer)));
        }
        foreach (var c in await db.Coaches.AsNoTracking().Where(c => c.IsDemo).Select(c => new { c.Id, c.FirstName, c.LastName, c.FirstNameLatin, c.LastNameLatin, c.Region }).ToListAsync(ct))
        {
            var k = FeedKeys.Make(AuthorKind.Coach, c.Id);
            if (exclude.Contains(k)) continue;
            var score = (c.Region == myRegion ? 3 : 0) + (active.Contains(k) ? 1 : 0);
            candidates.Add((new Suggestion(new PersonView(k, $"{c.FirstName} {c.LastName}", $"{c.FirstNameLatin} {c.LastNameLatin}", null),
                GreekNames.IsFemaleFirstName(c.FirstName) ? "Role_Coach_F" : "Role_Coach", c.Region, false), score, Tie(c.Id + 100_000, viewer)));
        }
        return candidates.OrderByDescending(c => c.Score).ThenBy(c => c.Tie).Take(Math.Clamp(take, 1, 5)).Select(c => c.S).ToList();

        // Deterministic spread so different viewers are not all shown the same lowest ids.
        static int Tie(int id, FeedViewer v) => (int)(((long)id * 7919 + v.EntityId * 104_729L) % 9973);
    }

    public async Task<int> NewPostsCountAsync(FeedViewer viewer, DateTimeOffset since, CancellationToken ct = default)
    {
        var q = db.Set<Post>().AsNoTracking().Where(p => !p.IsHidden && p.CreatedAt > since);
        if (viewer.Key is { } key)
        {
            var hidden = await db.Set<PostHide>().Where(h => h.ActorKey == key).Select(h => h.PostId).ToListAsync(ct);
            var blocked = await BlockedKeysAsync(key, ct);
            q = q.Where(p => p.AuthorKey != key && !hidden.Contains(p.Id) && !blocked.Contains(p.AuthorKey));
        }
        return await q.CountAsync(ct);
    }
}
