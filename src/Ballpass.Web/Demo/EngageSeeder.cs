using Ballpass.Web.Data;
using Ballpass.Web.Features.Engage;
using Ballpass.Web.Features.Feed;
using Bogus;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Demo;

/// <param name="TextEdits">Posts whose text gets hashtags appended (the tags below are exactly what parsing that text yields).</param>
public record EngageSeedSet(IReadOnlyList<Comment> Comments, IReadOnlyList<Kudos> ExtraKudos, IReadOnlyList<PostTag> Tags, IReadOnlyList<(Post Post, string Text)> TextEdits);

// Pure function of (demo data set, the feed seed's posts and kudos, reference time): one seeded Randomizer, no clock. Fictional Greek text, no insults,
// no links; comment authors are adult athletes and coaches of the demo data set. Every row is IsDemo.
public static class EngageSeeder
{
    public const int CommentTarget = 150;
    public const int KudosTarget = 250;
    private const int TaggedRecent = 12;   // of the 20 posts of the last 7 days
    private const int TaggedOlder = 4;     // of the 20 older posts: 16 of 40 = 40%

    // ---- comment texts ----

    private static readonly string[] Congrats =
    [
        "Μπράβο! Συνέχισε έτσι.", "Πολύ καλή δουλειά, φαίνεται η προσπάθεια.", "Συγχαρητήρια για την πρόοδο!", "Τέλεια! Καλή συνέχεια στην περίοδο.",
        "Εύγε, αξίζει η δουλειά που κάνεις.", "Μου αρέσει η νοοτροπία σου. Καλή συνέχεια!", "Πολύ ωραία, πάμε δυνατά!", "Καλή επιτυχία στην επόμενη αγωνιστική.",
        "Φαίνεται η σκληρή δουλειά στο γήπεδο.", "Ωραίο! Να είσαι πάντα καλά και υγιής.",
    ];

    private static readonly string[] StatTalk =
    [
        "Πολύ καλοί αριθμοί για την περίοδο!", "Ωραία ισορροπία ανάμεσα σε πόντους και ασίστ.", "Συγχαρητήρια για τη σταθερότητα στο παιχνίδι.",
        "Τα ποσοστά στο σουτ είναι εντυπωσιακά.", "Αξιόλογη χρονιά μέχρι τώρα. Προχωράς σωστά.", "Θα ήθελα να δω πώς θα εξελιχθούν τα ριμπάουντ.",
    ];

    private static readonly string[] TryoutQuestions =
    [
        "Μέχρι πότε γίνονται οι δηλώσεις συμμετοχής;", "Χρειάζεται να φέρουμε κάτι ιδιαίτερο στη δοκιμαστική;", "Ισχύει και για παίκτες με λίγη εμπειρία;",
        "Θα υπάρχει δοκιμαστική και για άλλες θέσεις;", "Ευχαριστούμε για την πρόσκληση, θα δηλώσω συμμετοχή.", "Πόση ώρα διαρκεί η προπόνηση;",
        "Υπάρχει διαθέσιμο πρόγραμμα για απογευματινές ώρες;",
    ];

    private static readonly string[] TryoutAnswers =
    [
        "Η δήλωση γίνεται μέχρι την προθεσμία της ανάρτησης. Καλώς να ορίσετε!", "Αρκεί αθλητικός εξοπλισμός και καλή διάθεση.",
        "Όλοι είναι ευπρόσδεκτοι να δοκιμαστούν.", "Η προπόνηση διαρκεί περίπου ενάμισι με δύο ώρες.", "Καλή επιτυχία σε όσους θα έρθουν, τα λέμε στο γήπεδο!",
    ];

    private static readonly string[] LookingReplies =
    [
        "Καλή επιτυχία στην αναζήτηση, θα βρεις ομάδα σύντομα!", "Αξίζει να κάνεις δοκιμαστική σε ομάδες της περιοχής σου.", "Πολύ καλό προφίλ. Καλή συνέχεια!",
        "Μην αγχώνεσαι, η σωστή ομάδα θα έρθει.", "Δες τις ανακοινώσεις για δοκιμαστικές, ίσως σε ενδιαφέρουν.", "Ευχαριστούμε για την ανάρτηση, θα το έχουμε υπόψη.",
    ];

    private static readonly string[] HighlightTalk =
    [
        "Τέλεια εκτέλεση!", "Ωραία ανάγνωση του παιχνιδιού.", "Αυτή η κίνηση θέλει πολλή προπόνηση!", "Καθαρή δουλειά, μπράβο.",
    ];

    private static readonly string[] CoachTips =
    [
        "Συμβουλή: δούλεψε καθημερινά τα βασικά, ντρίμπλα και πάσα και με τα δύο χέρια.", "Η συνέπεια στην προπόνηση είναι το μυστικό.",
        "Πρόσεξε και τον ύπνο και τη διατροφή, μετράνε περισσότερο απ' όσο νομίζεις.", "Καλή κίνηση. Πρόσεξε τη θέση των ποδιών στο σουτ.",
        "Δεκαπέντε λεπτά βολές μετά από κάθε προπόνηση κάνουν τη διαφορά.", "Συνέχισε να δουλεύεις στις αποφάσεις με την μπάλα στα χέρια.",
        "Το ομαδικό πνεύμα είναι το Α και το Ω. Συμφωνώ απόλυτα.", "Η άμυνα όντως κερδίζει παιχνίδια.",
    ];

    private static readonly string[] BadgeTalk = ["Συγχαρητήρια για το σήμα!", "Αναγνώριση που τη δικαιούσαι.", "Πολύ καλή δουλειά, συνέχισε."];

    // ---- hashtags ----

    private static readonly Dictionary<PostType, string[]> TagPools = new()
    {
        [PostType.LookingForTeam] = ["ψάχνωομάδα"],
        [PostType.TryoutCall] = ["tryouts"],
        [PostType.FreeText] = ["προπόνηση", "ομαδικότητα", "μπάσκετ"],
        [PostType.Highlight] = ["highlights", "καλάθι"],
        [PostType.StatUpdate] = ["στατιστικά"],
        [PostType.Badge] = ["επιτυχία"],
    };

    private static readonly string[] SecondTags = ["μπάσκετ", "προπόνηση", "δοκιμαστική"];

    private static IEnumerable<string> TextPool(Post post, bool coach) => coach
        ? post.Type switch
        {
            PostType.TryoutCall => TryoutAnswers.Concat(CoachTips),
            PostType.LookingForTeam => LookingReplies.Concat(CoachTips),
            PostType.Badge => BadgeTalk,
            _ => CoachTips.Concat(Congrats),
        }
        : post.Type switch
        {
            PostType.TryoutCall => TryoutQuestions,
            PostType.StatUpdate => StatTalk.Concat(Congrats),
            PostType.Highlight => HighlightTalk.Concat(Congrats),
            PostType.Badge => BadgeTalk.Concat(Congrats),
            PostType.LookingForTeam => LookingReplies,
            _ => Congrats,
        };

    public static EngageSeedSet Generate(DemoDataSet data, IReadOnlyList<Post> posts, IReadOnlyList<Kudos> existingKudos, DateTimeOffset reference)
    {
        var r = new Randomizer(DemoDataSeeder.Seed + 202);
        var adults = data.Athletes.Where(a => Safeguards.IsAdult(a.BirthYear, reference)).ToList();
        var authorKeys = posts.Select(p => p.AuthorKey).ToHashSet();

        // Everyone who acts: a recurring community (so names repeat like in a real feed), mostly athletes, some coaches.
        var community = r.Shuffle(adults.Where(a => authorKeys.Contains(FeedKeys.Make(AuthorKind.Athlete, a.Id)))).Take(14)
            .Concat(r.Shuffle(adults).Take(46)).DistinctBy(a => a.Id).Select(a => FeedKeys.Make(AuthorKind.Athlete, a.Id)).ToList();
        var coaches = r.Shuffle(data.Coaches).Take(14).Select(c => FeedKeys.Make(AuthorKind.Coach, c.Id)).ToList();
        // Kudos come from a wider pool: every adult athlete and every coach.
        var kudoPool = adults.Select(a => FeedKeys.Make(AuthorKind.Athlete, a.Id)).Concat(data.Coaches.Select(c => FeedKeys.Make(AuthorKind.Coach, c.Id))).ToList();

        static string[] Own(Post p) => p.PostedByCoachId is { } h ? [p.AuthorKey, FeedKeys.Make(AuthorKind.Coach, h)] : [p.AuthorKey];
        var latest = reference - TimeSpan.FromMinutes(2); // a seeded comment is never inside a real persona's 20 s cooldown window

        // ---- comments: 150, a few hot posts, some silent ones ----
        var weights = posts.Select(_ => r.Int(0, 6)).ToArray();
        foreach (var i in r.Shuffle(Enumerable.Range(0, posts.Count)).Take(5)) weights[i] = r.Int(9, 13);
        for (var guard = 0; weights.Sum() < CommentTarget && guard < 10_000; guard++) { var i = r.Int(0, weights.Length - 1); if (weights[i] < 14) weights[i]++; }
        for (var guard = 0; weights.Sum() > CommentTarget && guard < 10_000; guard++) { var i = r.Int(0, weights.Length - 1); if (weights[i] > 0) weights[i]--; }

        var comments = new List<Comment>();
        for (var i = 0; i < posts.Count; i++)
        {
            var post = posts[i];
            var own = Own(post);
            var used = new HashSet<string>();
            var times = Enumerable.Range(0, weights[i]).Select(_ => r.Int(8, 36 * 60)).OrderBy(m => m).ToList();
            foreach (var minutes in times)
            {
                var isCoach = r.Bool(0.28f);
                var actor = r.Shuffle(isCoach ? coaches : community).FirstOrDefault(k => !own.Contains(k) && !comments.Any(c => c.Post == post && c.AuthorKey == k));
                if (actor is null) continue;
                var text = r.Shuffle(TextPool(post, isCoach)).FirstOrDefault(t => used.Add(t));
                if (text is null) continue;
                var at = post.CreatedAt + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(r.Int(0, 59));
                if (at > latest) at = latest - TimeSpan.FromSeconds(r.Int(0, 600));
                if (at < post.CreatedAt) at = post.CreatedAt.AddMinutes(1);
                comments.Add(new Comment { Post = post, AuthorKey = actor, Text = text, CreatedAt = FeedKeys.Truncate(at), IsDemo = true });
            }
        }
        // Rounding above can leave a few short: top up on random posts until exactly CommentTarget.
        for (var guard = 0; comments.Count < CommentTarget && guard < 5_000; guard++)
        {
            var post = posts[r.Int(0, posts.Count - 1)];
            var isCoach = r.Bool(0.28f);
            var actor = r.Shuffle(isCoach ? coaches : community).FirstOrDefault(k => !Own(post).Contains(k) && !comments.Any(c => c.Post == post && c.AuthorKey == k));
            var text = r.Shuffle(TextPool(post, isCoach)).FirstOrDefault(t => !comments.Any(c => c.Post == post && c.Text == t));
            if (actor is null || text is null) continue;
            var at = post.CreatedAt + TimeSpan.FromMinutes(r.Int(8, 36 * 60));
            if (at > latest) at = latest - TimeSpan.FromSeconds(r.Int(0, 600));
            comments.Add(new Comment { Post = post, AuthorKey = actor, Text = text, CreatedAt = FeedKeys.Truncate(at), IsDemo = true });
        }

        // ---- kudos: ~250 in total; 4 viral posts (20-30), 8 mid, 14 low, the rest nearly silent ----
        var have = existingKudos.GroupBy(k => k.Post!).ToDictionary(g => g.Key, g => g.Count());
        int Have(Post p) => have.GetValueOrDefault(p);
        var order = r.Shuffle(posts).ToList();
        var ranges = new (int Min, int Max)[] { (20, 30), (7, 12), (2, 5), (0, 2) };
        var sizes = new[] { 4, 8, 14, int.MaxValue };
        var (min, max, target) = (new Dictionary<Post, int>(), new Dictionary<Post, int>(), new Dictionary<Post, int>());
        var idx = 0;
        for (var cls = 0; cls < ranges.Length; cls++)
            foreach (var p in order.Skip(idx).Take(sizes[cls]))
            {
                min[p] = Math.Max(Have(p), ranges[cls].Min);
                max[p] = Math.Max(Have(p), ranges[cls].Max);
                target[p] = r.Int(min[p], max[p]);
                idx++;
            }
        for (var guard = 0; target.Values.Sum() < KudosTarget && guard < 20_000; guard++) { var p = order[r.Int(0, order.Count - 1)]; if (target[p] < max[p]) target[p]++; }
        for (var guard = 0; target.Values.Sum() > KudosTarget && guard < 20_000; guard++) { var p = order[r.Int(0, order.Count - 1)]; if (target[p] > min[p]) target[p]--; }

        var taken = existingKudos.Select(k => (k.Post, k.ActorKey)).ToHashSet();
        var extra = new List<Kudos>();
        foreach (var post in posts)
        {
            var need = target[post] - Have(post);
            var own = Own(post);
            foreach (var actor in r.Shuffle(kudoPool).Where(k => !own.Contains(k) && !taken.Contains((post, k))).Take(Math.Max(0, need)))
            {
                var at = post.CreatedAt + TimeSpan.FromMinutes(r.Int(5, 2 * 24 * 60));
                if (at > latest) at = latest - TimeSpan.FromMinutes(r.Int(0, 20));
                extra.Add(new Kudos { Post = post, ActorKey = actor, CreatedAt = FeedKeys.Truncate(at < post.CreatedAt ? post.CreatedAt.AddMinutes(1) : at), IsDemo = true });
            }
        }

        // ---- hashtags: 12 of the 20 posts of the last 7 days, 4 of the 20 older ones ----
        var recent = posts.Where(p => reference - p.CreatedAt <= TimeSpan.FromDays(7)).ToList();
        var older = posts.Except(recent).ToList();
        var chosen = new List<Post>();
        chosen.AddRange(recent.Where(p => p.Type == PostType.LookingForTeam).Take(2));
        chosen.AddRange(recent.Where(p => p.Type == PostType.TryoutCall).Take(2));
        chosen.AddRange(r.Shuffle(recent.Except(chosen)).Take(Math.Max(0, TaggedRecent - chosen.Count)));
        chosen.AddRange(r.Shuffle(older).Take(TaggedOlder));

        var tags = new List<PostTag>();
        var edits = new List<(Post, string)>();
        foreach (var post in chosen)
        {
            var pool = TagPools[post.Type];
            var picked = new List<string> { pool[r.Int(0, pool.Length - 1)] };
            if (post.Type is PostType.LookingForTeam or PostType.TryoutCall or PostType.FreeText && r.Bool(0.4f))
            {
                var second = SecondTags[r.Int(0, SecondTags.Length - 1)];
                if (!picked.Contains(second)) picked.Add(second);
            }
            var hashtags = string.Join(" ", picked.Select(t => "#" + t));
            IReadOnlyList<(string Tag, string Label)> parsed;
            if (post.Type is PostType.LookingForTeam or PostType.FreeText && post.Text is { } text)
            {
                var edited = $"{text} {hashtags}";
                edits.Add((post, edited));
                parsed = PostTagParser.Parse(edited);
            }
            else parsed = PostTagParser.Parse(hashtags); // tryout, stat, badge, highlight: the tags live in the index only (their cards show them as chips)
            tags.AddRange(parsed.Select(t => new PostTag { Post = post, Tag = t.Tag, Label = t.Label }));
        }

        return new EngageSeedSet(comments.OrderBy(c => c.CreatedAt).ToList(), extra, tags, edits);
    }

    /// <summary>Adds the engage seed on top of the feed seed. Runs inside the caller's transaction, after <see cref="FeedSeeder.ReplaceAsync"/>.
    /// Comments and tags of the previous seed went with their posts (cascade).</summary>
    public static async Task ReplaceAsync(BallpassDbContext db, DemoDataSet data, DateTimeOffset reference, CancellationToken ct = default)
    {
        var posts = await db.Set<Post>().OrderBy(p => p.Id).ToListAsync(ct);
        var kudos = await db.Set<Kudos>().ToListAsync(ct);
        var seed = Generate(data, posts, kudos, reference);
        foreach (var (post, text) in seed.TextEdits) post.Text = text;
        db.Set<Comment>().AddRange(seed.Comments);
        db.Set<Kudos>().AddRange(seed.ExtraKudos);
        db.Set<PostTag>().AddRange(seed.Tags);
        await db.SaveChangesAsync(ct);
    }
}
