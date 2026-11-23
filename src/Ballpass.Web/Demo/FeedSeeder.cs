using Ballpass.Web.Data;
using Ballpass.Web.Features.Feed;
using Bogus;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Demo;

public record FeedSeedSet(IReadOnlyList<Post> Posts, IReadOnlyList<Kudos> Kudos, IReadOnlyList<Follow> Follows);

// Pure function of (demo data set, reference time): one seeded Randomizer, no clock. Re-running with a new reference time shifts every
// timestamp to "now". Fictional text in Greek and English (the *En columns); every row is IsDemo. Plan section 4: 40 posts, 25 authors (18 athletes, 5 coaches, 2 clubs).
public static class FeedSeeder
{
    private static readonly string[] LookingNotes =
    [
        "Ψάχνω ομάδα για την επόμενη περίοδο. Διαθεσιμότητα για δοκιμαστική προπόνηση.",
        "Θέλω να παίξω σε πιο απαιτητική κατηγορία. Δουλεύω καθημερινά στο σουτ.",
        "Αλλάζω πόλη και αναζητώ ομάδα με καλό κλίμα και σκληρή δουλειά.",
        "Προσφέρω ενέργεια στην άμυνα και στο γρήγορο παιχνίδι. Ανοιχτός σε προτάσεις.",
        "Μετά από διακοπή είμαι σε πλήρη φόρμα και ψάχνω ρόλο στο ρόστερ.",
        "Ψάχνω ομάδα με απογευματινές προπονήσεις λόγω σπουδών.",
        "Αναζητώ ομάδα που εμπιστεύεται νέους παίκτες και παίζει ομαδικά.",
        "Διαθεσιμότητα για αγώνες και προπονήσεις από τον επόμενο μήνα.",
    ];

    // English of each list below, same order.
    private static readonly string[] LookingNotesEn =
    [
        "Looking for a team for next season. Available for a tryout anytime.",
        "Ready to step up to a tougher league. Working on my shot every day.",
        "Moving cities and looking for a team with good vibes and a strong work ethic.",
        "I bring energy on defense and in transition. Open to offers.",
        "Back from a break, in full shape and looking for a spot on a roster.",
        "Looking for a team with evening practices so I can keep up with my studies.",
        "Looking for a team that trusts young players and moves the ball.",
        "Available for games and practices starting next month.",
    ];

    private static readonly string[] FreeTexts =
    [
        "Σκληρή προπόνηση σήμερα! Η δουλειά στο σουτ φέρνει αποτέλεσμα.",
        "Ευχαριστώ την ομάδα για μια υπέροχη περίοδο. Συνεχίζουμε δυνατά.",
        "Προπόνηση στις 7 το πρωί: καφές, μπάλα και καλή διάθεση.",
        "Το ομαδικό πνεύμα μετράει πριν από κάθε ταλέντο. Καλή αγωνιστική σε όλους!",
        "Μικρή υπενθύμιση: η άμυνα κερδίζει παιχνίδια. Δουλεύουμε στα βασικά.",
        "Ολοκληρώσαμε το πρώτο προσκλητήριο της περιόδου. Ευχαριστούμε όσους ήρθαν!",
        "Νέα εβδομάδα, νέοι στόχοι. Πάμε προπόνηση.",
        "Ευχαριστούμε τον κόσμο που ήρθε στο χθεσινό παιχνίδι. Η στήριξη μετράει.",
    ];

    private static readonly string[] FreeTextsEn =
    [
        "Tough practice today! All the shooting work is paying off.",
        "Thanks to the team for an amazing season. We keep pushing.",
        "7 a.m. practice: coffee, ball and good vibes.",
        "Team spirit beats talent every time. Good luck this weekend, everyone!",
        "Quick reminder: defense wins games. Back to the basics.",
        "First open tryout of the season is done. Thanks to everyone who came out!",
        "New week, new goals. Let's get to work.",
        "Thanks to everyone who came out to last night's game. Your support means a lot.",
    ];

    private static readonly string[] HighlightTitles =
    [
        "Τρίποντο στο φινάλε", "Κλέψιμο και καλάθι στην ανάπτυξη", "Ασίστ χωρίς να κοιτάξω",
        "Ριμπάουντ και γρήγορη μπάλα", "Καλάθι με φάουλ από τη γωνία", "Άμυνα στο ένας-εναντίον-ενός",
    ];

    private static readonly string[] HighlightTitlesEn =
    [
        "Three at the buzzer", "Steal and score on the break", "No-look assist",
        "Rebound and quick outlet", "And-one from the corner", "Lockdown one-on-one defense",
    ];

    private static readonly string[] HighlightTags = ["Σουτ", "Άμυνα", "Πάσα", "Ριμπάουντ", "Ταχύτητα", "Τρίποντο"];

    // Keys are localized in FeedResource (Badge_*).
    private static readonly string[] BadgeKeys = ["StatsCoachConfirmed", "ProfileComplete", "ThreeSeasons", "FirstSeasonLogged"];

    private static readonly string[] TryoutPositions = ["PG, SG", "SF, PF", "PF, C", "Όλες οι θέσεις"];
    private static readonly string[] TryoutPositionsEn = ["PG, SG", "SF, PF", "PF, C", "All positions"];

    public static FeedSeedSet Generate(DemoDataSet data, DateTimeOffset reference)
    {
        var r = new Randomizer(DemoDataSeeder.Seed + 101);
        var options = new FeedOptions();
        var today = DateOnly.FromDateTime(reference.UtcDateTime);

        // Authors. Athletes on a coach shortlist come first, so head coaches see them in "Following".
        var shortlisted = data.Shortlist.Select(s => s.Athlete!.Id).ToHashSet();
        var athletes = r.Shuffle(data.Athletes).OrderByDescending(a => shortlisted.Contains(a.Id)).ToList();
        var lookers = athletes.Where(a => a.LookingForTeam).Take(8).ToList();
        var statAuthors = athletes.Where(a => a.Stats.Count > 0 && !lookers.Contains(a)).Take(10).ToList();
        var athleteAuthors = lookers.Concat(statAuthors).ToList();
        var heads = r.Shuffle(data.Coaches.Where(c => c.Role == CoachRole.Head)).Take(7).ToList();
        var coachAuthors = heads.Take(5).ToList();
        var clubHeads = heads.Skip(5).ToList();

        T Pick<T>(IReadOnlyList<T> list) => list[r.Int(0, list.Count - 1)];

        Post Base(AuthorKind kind, int id, PostType type, bool system = false) => new()
        {
            AuthorKind = kind,
            AuthorId = id,
            AuthorKey = FeedKeys.Make(kind, id),
            Type = type,
            IsSystem = system,
            IsDemo = true,
        };
        Post ByAthlete(Athlete a, PostType type, bool system = false) => Base(AuthorKind.Athlete, a.Id, type, system);
        Post ByCoach(Coach c, PostType type) => Base(AuthorKind.Coach, c.Id, type);

        var posts = new List<Post>();

        for (var i = 0; i < lookers.Count; i++)
        {
            var a = lookers[i];
            var p = ByAthlete(a, PostType.LookingForTeam);
            p.Position = a.Position;
            p.Region = a.Region;
            p.AvailableFrom = today.AddDays(r.Int(0, 60));
            p.Text = LookingNotes[i % LookingNotes.Length];
            p.TextEn = LookingNotesEn[i % LookingNotesEn.Length];
            posts.Add(p);
        }

        void Tryout(Post p, Club club)
        {
            p.ClubId = club.Id;
            p.EventDate = today.AddDays(r.Int(4, 24));
            p.Deadline = p.EventDate.Value.AddDays(-r.Int(2, 4));
            p.Place = $"Γήπεδο ομάδας, {club.City}";
            p.PlaceEn = $"Home court, {GreekNames.Cities.First(c => c.Name == club.City).NameLatin}";
            var k = r.Int(0, TryoutPositions.Length - 1); // the same single draw as Pick(TryoutPositions)
            p.Positions = TryoutPositions[k];
            p.PositionsEn = TryoutPositionsEn[k];
            posts.Add(p);
        }
        foreach (var c in coachAuthors.Take(4)) Tryout(ByCoach(c, PostType.TryoutCall), c.Club!);
        foreach (var c in clubHeads)
        {
            var p = Base(AuthorKind.Club, c.Club!.Id, PostType.TryoutCall);
            p.PostedByCoachId = c.Id;
            Tryout(p, c.Club!);
        }

        foreach (var a in statAuthors)
        {
            var line = a.Stats.OrderByDescending(s => s.Season).First();
            var p = ByAthlete(a, PostType.StatUpdate, system: true);
            p.Season = line.Season;
            p.Games = line.Games;
            p.Points = line.Points;
            p.Rebounds = line.Rebounds;
            p.Assists = line.Assists;
            p.Source = line.Source;
            posts.Add(p);
        }

        for (var i = 0; i < 6; i++)
        {
            var p = ByAthlete(Pick(athleteAuthors), PostType.Highlight);
            p.Title = HighlightTitles[i];
            p.TitleEn = HighlightTitlesEn[i];
            p.Tag = HighlightTags[i];
            posts.Add(p);
        }

        for (var i = 0; i < BadgeKeys.Length; i++)
        {
            var p = ByAthlete(Pick(athleteAuthors), PostType.Badge, system: true);
            p.BadgeKey = BadgeKeys[i];
            posts.Add(p);
        }

        // Free text: the 5th coach (so all 5 coaches appear), one more coach, four athletes.
        var freeAuthors = new List<Post> { ByCoach(coachAuthors[4], PostType.FreeText), ByCoach(coachAuthors[0], PostType.FreeText) };
        for (var i = 0; i < 4; i++) freeAuthors.Add(ByAthlete(Pick(athleteAuthors), PostType.FreeText));
        for (var i = 0; i < freeAuthors.Count; i++)
        {
            freeAuthors[i].Text = FreeTexts[i];
            freeAuthors[i].TextEn = FreeTextsEn[i];
            posts.Add(freeAuthors[i]);
        }

        // Timing: 6 in the last 24 h, 14 at 2-7 days, 20 at 8-30 days; irregular gaps; none newer than 30 min (rate-limit window).
        var minutes = new List<int>();
        for (var i = 0; i < 6; i++) minutes.Add(r.Int(30, 24 * 60 - 1));
        for (var i = 0; i < 14; i++) minutes.Add(r.Int(2 * 24 * 60, 7 * 24 * 60 - 1));
        for (var i = 0; i < 20; i++) minutes.Add(r.Int(8 * 24 * 60, 30 * 24 * 60 - 1));
        var slots = r.Shuffle(minutes).ToList();
        for (var i = 0; i < posts.Count; i++)
        {
            posts[i].CreatedAt = FeedKeys.Truncate(reference - TimeSpan.FromMinutes(slots[i]) - TimeSpan.FromSeconds(r.Int(0, 59)));
            posts[i].SortAt = FeedKeys.Truncate(posts[i].CreatedAt + FeedRanking.Boost(posts[i].Type, posts[i].Source, options));
        }
        posts = posts.OrderBy(p => p.CreatedAt).ToList(); // ids ascend with time

        // Kudos: 45 on 25 posts (15 posts have none), at most 5 on one post.
        var kudosPosts = r.Shuffle(posts).Take(25).ToList();
        var counts = Enumerable.Repeat(1, kudosPosts.Count).ToArray();
        for (var extra = 45 - kudosPosts.Count; extra > 0;)
        {
            var i = r.Int(0, counts.Length - 1);
            if (counts[i] >= 5) continue;
            counts[i]++;
            extra--;
        }
        var actorPool = data.Coaches.Where(c => c.Role == CoachRole.Head).Select(c => FeedKeys.Make(AuthorKind.Coach, c.Id))
            .Concat(athleteAuthors.Select(a => FeedKeys.Make(AuthorKind.Athlete, a.Id))).ToList();
        var kudos = new List<Kudos>();
        for (var i = 0; i < kudosPosts.Count; i++)
        {
            var post = kudosPosts[i];
            var head = post.PostedByCoachId is { } h ? FeedKeys.Make(AuthorKind.Coach, h) : null;
            foreach (var actor in r.Shuffle(actorPool).Where(k => k != post.AuthorKey && k != head).Take(counts[i]))
            {
                var at = post.CreatedAt.AddMinutes(r.Int(5, 600));
                kudos.Add(new Kudos { Post = post, ActorKey = actor, CreatedAt = FeedKeys.Truncate(at > reference ? reference : at), IsDemo = true });
            }
        }

        // Follows: every shortlist entry is a follow; every athlete author follows 3 other authors.
        var follows = new List<Follow>();
        var seen = new HashSet<(string, string)>();
        void AddFollow(string from, string to, DateTimeOffset at)
        {
            if (from != to && seen.Add((from, to))) follows.Add(new Follow { FollowerKey = from, TargetKey = to, CreatedAt = FeedKeys.Truncate(at), IsDemo = true });
        }
        foreach (var s in data.Shortlist)
            AddFollow(FeedKeys.Make(AuthorKind.Coach, s.Coach!.Id), FeedKeys.Make(AuthorKind.Athlete, s.Athlete!.Id), reference - TimeSpan.FromDays(r.Int(1, 25)));
        var targets = athleteAuthors.Select(a => FeedKeys.Make(AuthorKind.Athlete, a.Id)).Concat(coachAuthors.Select(c => FeedKeys.Make(AuthorKind.Coach, c.Id))).ToList();
        foreach (var a in athleteAuthors)
        {
            var me = FeedKeys.Make(AuthorKind.Athlete, a.Id);
            foreach (var t in r.Shuffle(targets).Where(t => t != me).Take(3)) AddFollow(me, t, reference - TimeSpan.FromDays(r.Int(1, 20)));
        }

        return new FeedSeedSet(posts, kudos, follows);
    }

    /// <summary>Replaces all feed rows with the seed. Runs inside the caller's transaction, after demo athletes and coaches have ids.</summary>
    public static async Task ReplaceAsync(BallpassDbContext db, DemoDataSet data, DateTimeOffset reference, CancellationToken ct = default)
    {
        await db.Set<Kudos>().ExecuteDeleteAsync(ct);
        await db.Set<Report>().ExecuteDeleteAsync(ct);
        await db.Set<PostHide>().ExecuteDeleteAsync(ct);
        await db.Set<Post>().ExecuteDeleteAsync(ct);
        await db.Set<Follow>().ExecuteDeleteAsync(ct);
        await db.Set<Block>().ExecuteDeleteAsync(ct);

        var seed = Generate(data, reference);
        db.Set<Post>().AddRange(seed.Posts);
        db.Set<Kudos>().AddRange(seed.Kudos);
        db.Set<Follow>().AddRange(seed.Follows);
        await db.SaveChangesAsync(ct);
    }
}
