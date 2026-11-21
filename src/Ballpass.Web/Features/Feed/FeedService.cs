using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ballpass.Web.Features.Feed;

public class FeedService(BallpassDbContext db, TimeProvider time, IOptions<FeedOptions> options) : IFeedService
{
    private static readonly HashSet<string> Regions = GreekNames.Cities.Select(c => c.Region).ToHashSet();

    // ---- reading ----

    public async Task<FeedPageResult> GetFeedAsync(FeedViewer viewer, FeedTab tab, string? before, CancellationToken ct = default)
    {
        var q = db.Set<Post>().AsNoTracking().Where(p => !p.IsHidden);
        if (viewer.Key is { } key)
        {
            var hidden = await db.Set<PostHide>().Where(h => h.ActorKey == key).Select(h => h.PostId).ToListAsync(ct);
            var blocked = await BlockedKeysAsync(key, ct);
            q = q.Where(p => !hidden.Contains(p.Id) && !blocked.Contains(p.AuthorKey));
            if (tab == FeedTab.Following)
            {
                var followed = await db.Set<Follow>().Where(f => f.FollowerKey == key).Select(f => f.TargetKey).ToListAsync(ct);
                q = q.Where(p => followed.Contains(p.AuthorKey));
            }
        }

        var shown = 0;
        if (TryParseCursor(before, out var sortAt, out var lastId, out shown))
            q = q.Where(p => p.SortAt < sortAt || (p.SortAt == sortAt && p.Id < lastId));

        var take = Math.Min(FeedLimits.PageSize, FeedLimits.MaxItems - shown);
        if (take <= 0) return new FeedPageResult([], null);

        var rows = await q.OrderByDescending(p => p.SortAt).ThenByDescending(p => p.Id).Take(take + 1).ToListAsync(ct);
        var more = rows.Count > take && shown + take < FeedLimits.MaxItems;
        rows = rows.Take(take).ToList();
        var items = await HydrateAsync(rows, viewer, ct);
        var next = more && rows.Count > 0 ? $"{rows[^1].SortAt.UtcTicks}.{rows[^1].Id}.{shown + rows.Count}" : null;
        return new FeedPageResult(items, next);
    }

    public async Task<IReadOnlyList<FeedItem>> GetPreviewAsync(int count, CancellationToken ct = default)
    {
        var rows = await db.Set<Post>().AsNoTracking().Where(p => !p.IsHidden)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).Take(Math.Clamp(count, 1, 10)).ToListAsync(ct);
        return await HydrateAsync(rows, FeedViewer.Anonymous, ct);
    }

    private static bool TryParseCursor(string? cursor, out DateTimeOffset sortAt, out int id, out int shown)
    {
        sortAt = default; id = 0; shown = 0;
        var parts = cursor?.Split('.');
        if (parts is not { Length: 3 }
            || !long.TryParse(parts[0], out var ticks) || ticks < 0 || ticks > DateTimeOffset.MaxValue.Ticks
            || !int.TryParse(parts[1], out id) || !int.TryParse(parts[2], out shown))
            return false;
        shown = Math.Clamp(shown, 0, FeedLimits.MaxItems);
        sortAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }

    private async Task<List<string>> BlockedKeysAsync(string key, CancellationToken ct) =>
        await db.Set<Block>().Where(b => b.BlockerKey == key).Select(b => b.BlockedKey)
            .Concat(db.Set<Block>().Where(b => b.BlockedKey == key).Select(b => b.BlockerKey)).ToListAsync(ct);

    private async Task<IReadOnlyList<FeedItem>> HydrateAsync(List<Post> posts, FeedViewer viewer, CancellationToken ct)
    {
        if (posts.Count == 0) return [];
        var athleteIds = posts.Where(p => p.AuthorKind == AuthorKind.Athlete).Select(p => p.AuthorId).Distinct().ToList();
        var coachIds = posts.Where(p => p.AuthorKind == AuthorKind.Coach).Select(p => p.AuthorId).Distinct().ToList();
        var clubIds = posts.Where(p => p.AuthorKind == AuthorKind.Club).Select(p => p.AuthorId).Distinct().ToList();
        var athletes = await db.Athletes.AsNoTracking().Include(a => a.CurrentClub).Where(a => athleteIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var coaches = await db.Coaches.AsNoTracking().Include(c => c.Club).Where(c => coachIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var clubs = await db.Clubs.AsNoTracking().Where(c => clubIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var ids = posts.Select(p => p.Id).ToList();
        var own = new HashSet<string>();
        var counts = new Dictionary<int, int>();
        var kudoed = new HashSet<int>();
        var following = new HashSet<string>();
        var interested = new HashSet<int>();
        if (viewer.Key is { } key)
        {
            var ownIds = posts.Where(p => p.AuthorKey == key).Select(p => p.Id).ToList();
            counts = await db.Set<Kudos>().Where(k => ownIds.Contains(k.PostId)).GroupBy(k => k.PostId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            kudoed = (await db.Set<Kudos>().Where(k => k.ActorKey == key && ids.Contains(k.PostId)).Select(k => k.PostId).ToListAsync(ct)).ToHashSet();
            var authorKeys = posts.Select(p => p.AuthorKey).Distinct().ToList();
            following = (await db.Set<Follow>().Where(f => f.FollowerKey == key && authorKeys.Contains(f.TargetKey)).Select(f => f.TargetKey).ToListAsync(ct)).ToHashSet();
            if (viewer.IsCoach)
                interested = (await db.Interests.Where(i => i.CoachId == viewer.EntityId && athleteIds.Contains(i.AthleteId)).Select(i => i.AthleteId).ToListAsync(ct)).ToHashSet();
        }

        var items = new List<FeedItem>();
        foreach (var p in posts)
        {
            string nameEl, nameLatin, roleKey;
            string? slug = null, clubName = null;
            int? interestAthlete = null;
            switch (p.AuthorKind)
            {
                case AuthorKind.Athlete when athletes.TryGetValue(p.AuthorId, out var a):
                    (nameEl, nameLatin) = ($"{a.FirstName} {a.LastName}", $"{a.FirstNameLatin} {a.LastNameLatin}");
                    roleKey = a.Gender == Gender.Female ? "Role_Athlete_F" : "Role_Athlete";
                    (slug, clubName, interestAthlete) = (a.Slug, a.CurrentClub?.Name, a.Id);
                    break;
                case AuthorKind.Coach when coaches.TryGetValue(p.AuthorId, out var c):
                    (nameEl, nameLatin) = ($"{c.FirstName} {c.LastName}", $"{c.FirstNameLatin} {c.LastNameLatin}");
                    roleKey = GreekNames.IsFemaleFirstName(c.FirstName) ? "Role_Coach_F" : "Role_Coach";
                    clubName = c.Club?.Name;
                    break;
                case AuthorKind.Club when clubs.TryGetValue(p.AuthorId, out var club):
                    (nameEl, nameLatin, roleKey) = (club.Name, club.Name, "Role_Club");
                    break;
                default:
                    continue; // author row is gone (demo reset): skip rather than fail the page
            }
            var isOwn = viewer.Key is not null && p.AuthorKey == viewer.Key;
            items.Add(new FeedItem(p, nameEl, nameLatin, roleKey, slug, clubName, isOwn,
                kudoed.Contains(p.Id), isOwn ? counts.GetValueOrDefault(p.Id) : null,
                following.Contains(p.AuthorKey), interestAthlete is { } id && interested.Contains(id)));
        }
        return items;
    }

    // ---- posting ----

    private static readonly Dictionary<string, PostType[]> Allowed = new()
    {
        ["Athlete"] = [PostType.FreeText, PostType.LookingForTeam, PostType.Highlight],
        ["Coach"] = [PostType.FreeText, PostType.TryoutCall],
    };

    public async Task<PostOutcome> CreateAsync(FeedViewer viewer, NewPost input, CancellationToken ct = default)
    {
        if (viewer.Key is null || viewer.Role is null || !Allowed.TryGetValue(viewer.Role, out var types) || !types.Contains(input.Type))
            return PostOutcome.Forbidden;

        var now = FeedKeys.Truncate(time.GetUtcNow());
        var o = options.Value;
        Athlete? athlete = null;
        Coach? coach = null;
        if (viewer.IsAthlete)
        {
            athlete = await db.Athletes.FirstOrDefaultAsync(a => a.Id == viewer.EntityId && a.IsDemo, ct);
            if (athlete is null) return PostOutcome.Forbidden;
            if (!Safeguards.IsAdult(athlete.BirthYear, now)) return PostOutcome.Minor; // adults only, enforced server-side
        }
        else
        {
            coach = await db.Coaches.FirstOrDefaultAsync(c => c.Id == viewer.EntityId && c.IsDemo, ct);
            if (coach is null) return PostOutcome.Forbidden;
        }

        if (input.Type == PostType.LookingForTeam && !input.LookingOn)
        {
            athlete!.LookingForTeam = false;
            await db.SaveChangesAsync(ct);
            return PostOutcome.SwitchedOff;
        }

        var post = new Post
        {
            AuthorKind = viewer.IsAthlete ? AuthorKind.Athlete : AuthorKind.Coach,
            AuthorId = viewer.EntityId,
            AuthorKey = viewer.Key,
            Type = input.Type,
            CreatedAt = now,
            IsDemo = true,
        };
        var invalid = Fill(post, input, athlete, coach, DateOnly.FromDateTime(now.UtcDateTime));
        if (invalid is not null) return invalid.Value;

        var since = now - TimeSpan.FromSeconds(o.PostCooldownSeconds);
        if (await db.Set<Post>().AnyAsync(p => p.AuthorKey == viewer.Key && !p.IsSystem && p.CreatedAt > since, ct))
            return PostOutcome.RateLimited;

        post.SortAt = FeedKeys.Truncate(now + FeedRanking.Boost(post.Type, post.Source, o));
        db.Set<Post>().Add(post);
        if (post.Type == PostType.LookingForTeam) athlete!.LookingForTeam = true;
        await db.SaveChangesAsync(ct);
        return PostOutcome.Posted;
    }

    /// <summary>Validates and copies the fields of the post type. Null means valid.</summary>
    private static PostOutcome? Fill(Post post, NewPost input, Athlete? athlete, Coach? coach, DateOnly today)
    {
        if (input.Text is { Length: > 2000 } || input.Title is { Length: > 2000 } || input.Place is { Length: > 2000 }) return PostOutcome.TooLong;
        switch (input.Type)
        {
            case PostType.FreeText:
                post.Text = Safeguards.Clean(input.Text);
                if (post.Text is null) return PostOutcome.Invalid;
                if (post.Text.Length > FeedLimits.TextMax) return PostOutcome.TooLong;
                break;
            case PostType.LookingForTeam:
                post.Text = Safeguards.Clean(input.Text);
                if (post.Text is { Length: > FeedLimits.NoteMax }) return PostOutcome.TooLong;
                post.Position = input.Position ?? athlete!.Position;
                post.Region = input.Region is { } region && Regions.Contains(region) ? region : athlete!.Region;
                post.AvailableFrom = input.AvailableFrom;
                break;
            case PostType.Highlight:
                post.Title = Safeguards.Clean(input.Title);
                if (post.Title is null) return PostOutcome.Invalid;
                if (post.Title.Length > FeedLimits.TitleMax) return PostOutcome.TooLong;
                post.Tag = athlete!.Position.ToString();
                break;
            case PostType.TryoutCall:
                post.Place = Safeguards.Clean(input.Place);
                post.Positions = Safeguards.Clean(input.Positions);
                if (post.Place is null || input.EventDate is not { } date || date < today) return PostOutcome.Invalid;
                if (input.Deadline is { } deadline && deadline > date) return PostOutcome.Invalid;
                if (post.Place.Length > FeedLimits.PlaceMax || post.Positions is { Length: > FeedLimits.PositionsMax }) return PostOutcome.TooLong;
                post.ClubId = coach!.ClubId;
                post.EventDate = date;
                post.Deadline = input.Deadline;
                break;
            default:
                return PostOutcome.Forbidden;
        }
        return null;
    }

    // ---- kudos ----

    public async Task<bool> SetKudosAsync(FeedViewer viewer, int postId, bool on, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return false;
        var post = await VisiblePostAsync(viewer, postId, ct);
        if (post is null) return false;
        if (post.AuthorKey == key) return true; // no kudos on your own post: nothing to do

        if (!on)
        {
            await db.Set<Kudos>().Where(k => k.PostId == postId && k.ActorKey == key).ExecuteDeleteAsync(ct);
            return true;
        }
        if (!await db.Set<Kudos>().AnyAsync(k => k.PostId == postId && k.ActorKey == key, ct))
            await TryAddAsync(new Kudos { PostId = postId, ActorKey = key, CreatedAt = time.GetUtcNow() }, ct);
        return true;
    }

    // ---- safeguards ----

    public async Task<ModOutcome> ReportAsync(FeedViewer viewer, int postId, string? reason, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return ModOutcome.Forbidden;
        var post = await VisiblePostAsync(viewer, postId, ct);
        if (post is null) return ModOutcome.NotFound;
        if (post.AuthorKey == key) return ModOutcome.Forbidden;
        if (reason is null || !Safeguards.ReportReasons.Contains(reason)) return ModOutcome.Invalid;

        if (!await db.Set<Report>().AnyAsync(r => r.PostId == postId && r.ReporterKey == key, ct))
            await TryAddAsync(new Report { PostId = postId, ReporterKey = key, Reason = reason, CreatedAt = time.GetUtcNow() }, ct);
        await HideForAsync(postId, key, ct); // hidden for the reporter at once; the reported author is never notified

        var reporters = await db.Set<Report>().Where(r => r.PostId == postId).Select(r => r.ReporterKey).Distinct().CountAsync(ct);
        if (reporters >= options.Value.ReportHideThreshold)
            await db.Set<Post>().Where(p => p.Id == postId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsHidden, true), ct);
        return ModOutcome.Ok;
    }

    public async Task<ModOutcome> HideAsync(FeedViewer viewer, int postId, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return ModOutcome.Forbidden;
        if (await VisiblePostAsync(viewer, postId, ct) is null) return ModOutcome.NotFound;
        await HideForAsync(postId, key, ct);
        return ModOutcome.Ok;
    }

    public async Task<ModOutcome> BlockAuthorAsync(FeedViewer viewer, int postId, CancellationToken ct = default)
    {
        if (viewer.Key is not { } key) return ModOutcome.Forbidden;
        var post = await VisiblePostAsync(viewer, postId, ct);
        if (post is null) return ModOutcome.NotFound;
        var target = post.AuthorKey;
        if (target == key) return ModOutcome.Forbidden;

        if (!await db.Set<Block>().AnyAsync(b => b.BlockerKey == key && b.BlockedKey == target, ct))
            await TryAddAsync(new Block { BlockerKey = key, BlockedKey = target, CreatedAt = time.GetUtcNow() }, ct);
        await db.Set<Follow>().Where(f => (f.FollowerKey == key && f.TargetKey == target) || (f.FollowerKey == target && f.TargetKey == key)).ExecuteDeleteAsync(ct);
        return ModOutcome.Ok;
    }

    private async Task HideForAsync(int postId, string key, CancellationToken ct)
    {
        if (!await db.Set<PostHide>().AnyAsync(h => h.PostId == postId && h.ActorKey == key, ct))
            await TryAddAsync(new PostHide { PostId = postId, ActorKey = key }, ct);
    }

    /// <summary>The post, unless it does not exist, is hidden for everyone, or its author and the viewer blocked each other.</summary>
    private async Task<Post?> VisiblePostAsync(FeedViewer viewer, int postId, CancellationToken ct)
    {
        var post = await db.Set<Post>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && !p.IsHidden, ct);
        if (post is null || viewer.Key is not { } key) return post;
        var blocked = await db.Set<Block>().AnyAsync(b => (b.BlockerKey == key && b.BlockedKey == post.AuthorKey) || (b.BlockerKey == post.AuthorKey && b.BlockedKey == key), ct);
        return blocked ? null : post;
    }

    /// <summary>Adds a row; a concurrent duplicate (unique index) counts as already done.</summary>
    private async Task TryAddAsync<T>(T entity, CancellationToken ct) where T : class
    {
        db.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
        }
    }
}

public class FollowService(BallpassDbContext db, TimeProvider time) : IFollowService
{
    public async Task<bool> FollowAsync(string followerKey, string targetKey, CancellationToken ct = default)
    {
        if (followerKey == targetKey || !await ExistsAsync(followerKey, ct) || !await ExistsAsync(targetKey, ct)) return false;
        if (await db.Set<Block>().AnyAsync(b => (b.BlockerKey == followerKey && b.BlockedKey == targetKey) || (b.BlockerKey == targetKey && b.BlockedKey == followerKey), ct))
            return false;
        if (await db.Set<Follow>().AnyAsync(f => f.FollowerKey == followerKey && f.TargetKey == targetKey, ct)) return true;

        db.Add(new Follow { FollowerKey = followerKey, TargetKey = targetKey, CreatedAt = time.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear(); // concurrent duplicate: already following
        }
        return true;
    }

    public Task UnfollowAsync(string followerKey, string targetKey, CancellationToken ct = default) =>
        db.Set<Follow>().Where(f => f.FollowerKey == followerKey && f.TargetKey == targetKey).ExecuteDeleteAsync(ct);

    public Task<bool> IsFollowingAsync(string followerKey, string targetKey, CancellationToken ct = default) =>
        db.Set<Follow>().AnyAsync(f => f.FollowerKey == followerKey && f.TargetKey == targetKey, ct);

    private async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        if (!FeedKeys.TryParse(key, out var kind, out var id)) return false;
        return kind switch
        {
            AuthorKind.Athlete => await db.Athletes.AnyAsync(a => a.Id == id && a.IsDemo, ct),
            AuthorKind.Coach => await db.Coaches.AnyAsync(c => c.Id == id && c.IsDemo, ct),
            _ => await db.Clubs.AnyAsync(c => c.Id == id && c.IsDemo, ct),
        };
    }
}
