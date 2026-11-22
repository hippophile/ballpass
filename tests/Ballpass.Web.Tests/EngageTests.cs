using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Engage;
using Ballpass.Web.Features.Feed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-engage/specs/engage/spec.md
// Every test that posts a comment uses its own persona (1 comment / 20 s per persona) and its own fresh post, so test order does not matter.
public class EngageHarness(BallpassAppFactory app)
{
    public FeedHarness H { get; } = new(app);
    public BallpassAppFactory App => app;
    private static int counter;

    public static string Text(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> TagOf = new();

    /// <summary>A free-text post by a head coach. 40 days old by default, so it never competes with the seed for the 60 visible feed places; it is found
    /// through its own unique hashtag (<see cref="Card"/> does that).</summary>
    public int NewPost(int coachSkip, string? text = null, DateTimeOffset? at = null, bool hidden = false) => H.Db(db =>
    {
        var coachId = db.Coaches.Where(c => c.IsDemo && c.Role == CoachRole.Head).OrderBy(c => c.Id).Skip(coachSkip).Select(c => c.Id).First();
        var when = at ?? DateTimeOffset.UtcNow.AddDays(-40).AddSeconds(-Interlocked.Increment(ref counter));
        var post = new Post
        {
            AuthorKind = AuthorKind.Coach, AuthorId = coachId, AuthorKey = $"coach:{coachId}", Type = PostType.FreeText,
            Text = text ?? "Δοκιμαστική ανάρτηση για σχόλια.", CreatedAt = FeedKeys.Truncate(when), SortAt = FeedKeys.Truncate(when), IsDemo = true, IsHidden = hidden,
        };
        db.Set<Post>().Add(post);
        var tag = "u" + Guid.NewGuid().ToString("N")[..20];
        db.Set<PostTag>().Add(new PostTag { Post = post, Tag = tag, Label = tag });
        db.SaveChanges();
        TagOf[post.Id] = tag;
        return post.Id;
    });

    public static string TagUrl(int postId) => TagOf.TryGetValue(postId, out var tag) ? $"/feed?tag={tag}" : "/feed";

    public string AuthorKeyOf(int postId) => H.Db(db => db.Set<Post>().Where(p => p.Id == postId).Select(p => p.AuthorKey).First());

    public async Task<HttpClient> AuthorOf(int postId)
    {
        FeedKeys.TryParse(AuthorKeyOf(postId), out _, out var id);
        return await H.LoginAsync("coach", id);
    }

    public int Athlete(int skip) => H.AthleteId(skip);

    public int AddComment(int postId, string authorKey, string text, DateTimeOffset? at = null) => H.Db(db =>
    {
        var c = new Comment { PostId = postId, AuthorKey = authorKey, Text = text, CreatedAt = FeedKeys.Truncate(at ?? DateTimeOffset.UtcNow), IsDemo = true };
        db.Set<Comment>().Add(c);
        db.SaveChanges();
        return c.Id;
    });

    public Task<HttpResponseMessage> Comment(HttpClient client, int postId, string text) =>
        H.PostAsync(client, "/feed/comment", new() { ["postId"] = postId.ToString(), ["text"] = text, ["returnUrl"] = "/feed" });

    public int CommentsBy(string key, int postId) => H.Db(db => db.Set<Comment>().Count(c => c.AuthorKey == key && c.PostId == postId));

    /// <summary>Follows "show more" until the card of the post shows up. Returns the card html.</summary>
    public async Task<string> Card(HttpClient client, int postId, string? start = null)
    {
        string? url = start ?? TagUrl(postId);
        for (var i = 0; i < 10 && url is not null; i++)
        {
            var html = await FeedHarness.Html(client, url);
            var card = FeedHarness.Cards(html).FirstOrDefault(c => c.Id == postId);
            if (card.Html is not null) return card.Html;
            var more = Regex.Match(html, "<a[^>]*data-feed-more[^>]*href=\"([^\"]+)\"");
            url = more.Success ? more.Groups[1].Value : null;
        }
        throw new Xunit.Sdk.XunitException($"post {postId} not found on any page");
    }

    public static IEnumerable<int> CommentIds(string cardHtml) =>
        Regex.Matches(cardHtml, "data-comment-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value));

    public static string Name(Athlete a) => $"{a.FirstName} {a.LastName}";
}

[Collection("db")]
public class EngageTests(BallpassAppFactory app)
{
    private readonly EngageHarness e = new(app);
    private FeedHarness h => e.H;

    // ---- hashtag parsing ----

    [Fact]
    public void Parser_is_case_and_accent_insensitive_and_dedupes()
    {
        var tags = PostTagParser.Parse("Μέρα #ΠΡΟΠΟΝΗΣΗ και πάλι #προπόνηση, και #Προπόνηση!");

        var single = Assert.Single(tags);
        Assert.Equal("προπονηση", single.Tag);
        Assert.Equal(PostTagParser.Fold("ΠΡΟΠΟΝΗΣΗ"), PostTagParser.Fold("προπόνηση"));
        Assert.Equal(PostTagParser.Fold("Ψάχνω"), PostTagParser.Fold("ψαχνω"));
    }

    [Fact]
    public void Parser_keeps_the_typed_lower_case_label_and_folds_final_sigma()
    {
        var tags = PostTagParser.Parse("#Προπόνηση #TRYOUTS");

        Assert.Equal(["προπονηση", "tryouts"], tags.Select(t => t.Tag));
        Assert.Equal(["προπόνηση", "tryouts"], tags.Select(t => t.Label));
        Assert.Equal(PostTagParser.Fold("ΟΜΑΔΟΣ"), PostTagParser.Fold("ομάδος"));
    }

    [Fact]
    public void Parser_takes_at_most_3_tags_in_order_of_appearance()
    {
        var tags = PostTagParser.Parse("#aa #bb #cc #dd #ee");

        Assert.Equal(["aa", "bb", "cc"], tags.Select(t => t.Tag));
    }

    [Fact]
    public void Parser_reads_text_and_title_together_but_still_caps_at_3()
    {
        var tags = PostTagParser.Parse("#aa #bb", null, "#cc #dd");

        Assert.Equal(3, tags.Count);
    }

    [Theory]
    [InlineData("ψάχνω#nothash")]      // glued to a word
    [InlineData("# space")]
    [InlineData("#1")]                  // too short, no letter
    [InlineData("#12345")]              // digits only
    [InlineData("#_")]
    [InlineData("##")]
    [InlineData("το email a@b.gr#xx")]  // glued
    public void Parser_ignores_things_that_are_not_hashtags(string text) => Assert.Empty(PostTagParser.Parse(text));

    [Fact]
    public void Parser_accepts_greek_and_latin_letters_digits_and_underscore_and_stops_at_punctuation()
    {
        var tags = PostTagParser.Parse("#ψάχνωομάδα, #tryouts! (#u18_b) #a1");

        Assert.Equal(["ψαχνωομαδα", "tryouts", "u18_b"], tags.Select(t => t.Tag));
        Assert.Equal("ψάχνωομάδα", tags[0].Label);
    }

    [Fact]
    public void Parser_rejects_tags_longer_than_40_characters()
    {
        Assert.Empty(PostTagParser.Parse("#" + new string('α', 41)));
        Assert.Single(PostTagParser.Parse("#" + new string('α', 40)));
    }

    [Fact]
    public void Fold_of_null_or_blank_is_null()
    {
        Assert.Null(PostTagParser.Fold(null));
        Assert.Null(PostTagParser.Fold("  "));
        Assert.Equal("προπονηση", PostTagParser.Fold("#Προπόνηση "));
    }

    // ---- tags: indexing, links, filter ----

    [Fact]
    public async Task Posting_free_text_indexes_at_most_3_hashtags()
    {
        var athleteId = e.Athlete(70);
        var athlete = await h.LoginAsync("athlete", athleteId);

        await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "FreeText", ["text"] = "Μέρα προπόνησης #ΠΡΟΠΟΝΗΣΗ #tryouts #x1 #τρίτο #τέταρτο", ["returnUrl"] = "/feed" });

        var postId = h.Db(db => db.Set<Post>().Where(p => p.AuthorKey == $"athlete:{athleteId}").OrderByDescending(p => p.Id).Select(p => p.Id).First());
        var tags = h.Db(db => db.Set<PostTag>().Where(t => t.PostId == postId).OrderBy(t => t.Id).Select(t => t.Tag).ToList());
        Assert.Equal(["προπονηση", "tryouts", "x1"], tags);
    }

    [Fact]
    public async Task Cards_render_their_hashtags_as_links_to_the_tag_filter()
    {
        var postId = e.NewPost(1, "Ψάχνουμε παίκτες #tryouts");
        h.Db(db => { db.Set<PostTag>().Add(new PostTag { PostId = postId, Tag = "tryouts", Label = "tryouts" }); db.SaveChanges(); return 0; });

        var card = await e.Card(app.Client(), postId);

        Assert.Matches("<a[^>]*href=\"/feed\\?tag=tryouts\"[^>]*>", card);
        Assert.Contains("#tryouts", EngageHarness.Text(card));
    }

    [Fact]
    public async Task Tag_filter_shows_only_tagged_posts_with_paging_and_no_duplicates()
    {
        const string tag = "pagingtest";
        var ids = Enumerable.Range(0, 12).Select(i => e.NewPost(2, $"Παλιά ανάρτηση {i} #{tag}", DateTimeOffset.UtcNow.AddDays(-40).AddMinutes(-i))).ToList();
        h.Db(db => { db.Set<PostTag>().AddRange(ids.Select(id => new PostTag { PostId = id, Tag = tag, Label = tag })); db.SaveChanges(); return 0; });

        var cards = await FeedHarness.AllPages(app.Client(), $"/feed?tag={tag}");

        Assert.Equal(12, cards.Count);
        Assert.Equal(ids.OrderBy(i => i), cards.Select(c => c.Id).OrderBy(i => i));
        Assert.Equal(cards.Count, cards.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public async Task Tag_filter_matches_case_and_accent_insensitively()
    {
        var a = e.NewPost(3, "Σήμερα #ΠΡΟΠΟΝΗΣΗ");
        h.Db(db => { db.Set<PostTag>().Add(new PostTag { PostId = a, Tag = "προπονηση", Label = "προπονηση" }); db.SaveChanges(); return 0; });

        var viaAccent = await FeedHarness.AllPages(app.Client(), "/feed?tag=" + Uri.EscapeDataString("προπόνηση"));
        var viaUpper = await FeedHarness.AllPages(app.Client(), "/feed?tag=" + Uri.EscapeDataString("ΠΡΟΠΟΝΗΣΗ"));

        Assert.Contains(viaAccent, c => c.Id == a);
        Assert.Contains(viaUpper, c => c.Id == a);
        Assert.Equal(viaAccent.Select(c => c.Id).OrderBy(i => i), viaUpper.Select(c => c.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task Unknown_tag_shows_an_empty_state_not_an_error()
    {
        var response = await app.Client().GetAsync("/feed?tag=" + Uri.EscapeDataString("δενυπαρχειτιποτα"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-feed-empty", await response.Content.ReadAsStringAsync());
    }

    // ---- trends ----

    [Fact]
    public async Task Trends_counts_equal_the_database_counts_of_the_last_7_days_and_skip_hidden_posts()
    {
        var hidden = Enumerable.Range(0, 4).Select(i => e.NewPost(4, $"Κρυφή {i} #hiddenonly", DateTimeOffset.UtcNow.AddHours(-1 - i), hidden: true)).ToList();
        h.Db(db => { db.Set<PostTag>().AddRange(hidden.Select(id => new PostTag { PostId = id, Tag = "hiddenonly", Label = "hiddenonly" })); db.SaveChanges(); return 0; });
        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var expected = h.Db(db => db.Set<PostTag>().Where(t => !t.Post!.IsHidden && t.Post.CreatedAt >= since)
            .GroupBy(t => t.Tag).Select(g => new { g.Key, N = g.Count() }).ToList()).ToDictionary(x => x.Key, x => x.N);

        var html = await FeedHarness.Html(app.Client(), "/feed");

        var section = Regex.Match(html, "data-engage=\"trends\".*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(section);
        var shown = Regex.Matches(section, "data-tag-key=\"([^\"]+)\"[^>]*data-tag-count=\"(\\d+)\"")
            .Select(m => (Key: m.Groups[1].Value, N: int.Parse(m.Groups[2].Value))).DistinctBy(x => x.Key).ToList();
        Assert.InRange(shown.Count, 3, 6);
        Assert.DoesNotContain(shown, s => s.Key == "hiddenonly");
        Assert.All(shown, s => Assert.Equal(expected[s.Key], s.N));
        Assert.Equal(shown.Select(s => s.N), shown.Select(s => s.N).OrderByDescending(n => n)); // sorted by count
        Assert.Equal(expected.Values.OrderByDescending(n => n).Take(shown.Count), shown.Select(s => s.N)); // the top ones
        Assert.Matches("href=\"/feed\\?tag=", section);
    }

    // ---- comments: create ----

    [Fact]
    public async Task Signed_in_athlete_comments_and_it_shows_with_counts_and_returns_to_the_post()
    {
        var postId = e.NewPost(5);
        var id = e.Athlete(41);
        var athlete = await h.LoginAsync("athlete", id);

        var response = await e.Comment(athlete, postId, "Μπράβο για τη δουλειά!");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"#post-{postId}", response.Headers.Location!.OriginalString);
        Assert.Equal(1, e.CommentsBy($"athlete:{id}", postId));
        var card = await e.Card(athlete, postId);
        Assert.Contains("Μπράβο για τη δουλειά!", EngageHarness.Text(card));
        Assert.Contains("data-comment-count=\"1\"", card);
    }

    [Fact]
    public async Task Coaches_comment_too()
    {
        var postId = e.NewPost(6);
        var coachId = h.CoachId(12);
        var coach = await h.LoginAsync("coach", coachId);

        await e.Comment(coach, postId, "Καλή επιτυχία στη δοκιμαστική.");

        Assert.Equal(1, e.CommentsBy($"coach:{coachId}", postId));
    }

    [Fact]
    public async Task Anonymous_comment_is_403_and_stores_nothing()
    {
        var postId = e.NewPost(7);

        var response = await h.PostAsync(app.Client(), "/feed/comment", new() { ["postId"] = postId.ToString(), ["text"] = "Γεια", ["returnUrl"] = "/feed" }, tokenPage: "/demo");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, h.Db(db => db.Set<Comment>().Count(c => c.PostId == postId)));
    }

    [Theory]
    [InlineData("/feed/comment")]
    [InlineData("/feed/comment/delete")]
    [InlineData("/feed/comment/hide")]
    [InlineData("/feed/comment/report")]
    public async Task Comment_posts_without_antiforgery_token_are_400(string path)
    {
        var athlete = await h.LoginAsync("athlete", e.Athlete(42));

        var response = await h.PostAsync(athlete, path, new() { ["postId"] = "1", ["commentId"] = "1", ["text"] = "x" }, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Comment_on_an_unknown_post_is_404()
    {
        var athlete = await h.LoginAsync("athlete", e.Athlete(43));

        var response = await e.Comment(athlete, 99999999, "Γεια");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Urls_are_stripped_and_text_is_capped_at_280()
    {
        var postId = e.NewPost(8);
        var a = e.Athlete(44);
        var b = e.Athlete(45);
        var c = e.Athlete(46);
        var d = e.Athlete(47);

        await e.Comment(await h.LoginAsync("athlete", a), postId, "Δείτε https://evil.example/x και www.foo.gr τέλος");
        await e.Comment(await h.LoginAsync("athlete", b), postId, new string('α', 280));
        await e.Comment(await h.LoginAsync("athlete", c), postId, new string('β', 281));
        await e.Comment(await h.LoginAsync("athlete", d), postId, "   ");

        var texts = h.Db(db => db.Set<Comment>().Where(x => x.PostId == postId).OrderBy(x => x.Id).Select(x => x.Text).ToList());
        Assert.Equal(2, texts.Count);
        Assert.DoesNotContain("evil", texts[0]);
        Assert.DoesNotContain("foo.gr", texts[0]);
        Assert.Contains("Δείτε", texts[0]);
        Assert.Equal(280, texts[1].Length);
        Assert.Equal(0, e.CommentsBy($"athlete:{c}", postId));
        Assert.Equal(0, e.CommentsBy($"athlete:{d}", postId));
    }

    [Fact]
    public async Task One_comment_per_20_seconds_per_persona()
    {
        var postId = e.NewPost(9);
        var id = e.Athlete(48);
        var athlete = await h.LoginAsync("athlete", id);
        var other = await h.LoginAsync("athlete", e.Athlete(49));

        await e.Comment(athlete, postId, "Πρώτο");
        var second = await e.Comment(athlete, postId, "Δεύτερο");
        await e.Comment(other, postId, "Άλλος αθλητής");

        Assert.Equal(1, e.CommentsBy($"athlete:{id}", postId));
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Contains("cs=rate", second.Headers.Location!.OriginalString);
        Assert.Equal(2, h.Db(db => db.Set<Comment>().Count(c => c.PostId == postId)));
    }

    [Fact]
    public async Task Service_rejects_a_minor_commenter_and_accepts_an_adult()
    {
        await using var db = await UnitDb.CreateAsync();
        var minor = new Athlete { Slug = "minor-c", FirstName = "Α", LastName = "Β", FirstNameLatin = "A", LastNameLatin = "B", BirthYear = 2012, City = "Αθήνα", Region = "Αττική", IsDemo = true };
        var adult = new Athlete { Slug = "adult-c", FirstName = "Γ", LastName = "Δ", FirstNameLatin = "G", LastNameLatin = "D", BirthYear = 1998, City = "Αθήνα", Region = "Αττική", IsDemo = true };
        db.AddRange(minor, adult);
        await db.SaveChangesAsync();
        var now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        var post = new Post { AuthorKind = AuthorKind.Athlete, AuthorId = adult.Id, AuthorKey = $"athlete:{adult.Id}", Type = PostType.FreeText, Text = "Γεια", CreatedAt = now.AddHours(-1), SortAt = now.AddHours(-1), IsDemo = true };
        db.Add(post);
        await db.SaveChangesAsync();
        var service = new EngageService(db, new UnitDb.FixedTime(now), Options.Create(new FeedOptions()), Options.Create(new EngageOptions()));

        var rejected = await service.AddCommentAsync(new FeedViewer($"athlete:{minor.Id}", "Athlete", minor.Id, null), post.Id, "Γεια σας");
        var accepted = await service.AddCommentAsync(new FeedViewer($"athlete:{adult.Id}", "Athlete", adult.Id, null), post.Id, "Γεια σας");

        Assert.Equal(CommentOutcome.Minor, rejected);
        Assert.Equal(CommentOutcome.Posted, accepted);
        Assert.Equal(1, await db.Set<Comment>().CountAsync());
    }

    [Fact]
    public async Task Blocked_users_cannot_comment_on_each_others_posts_in_either_direction()
    {
        var postByAuthor = e.NewPost(10);
        var authorKey = e.AuthorKeyOf(postByAuthor);
        var blockedByAuthor = e.Athlete(50);
        var blockerOfAuthor = e.Athlete(51);
        h.Db(db =>
        {
            db.Set<Block>().Add(new Block { BlockerKey = authorKey, BlockedKey = $"athlete:{blockedByAuthor}", CreatedAt = DateTimeOffset.UtcNow, IsDemo = true });
            db.Set<Block>().Add(new Block { BlockerKey = $"athlete:{blockerOfAuthor}", BlockedKey = authorKey, CreatedAt = DateTimeOffset.UtcNow, IsDemo = true });
            db.SaveChanges();
            return 0;
        });

        var r1 = await e.Comment(await h.LoginAsync("athlete", blockedByAuthor), postByAuthor, "Γεια");
        var r2 = await e.Comment(await h.LoginAsync("athlete", blockerOfAuthor), postByAuthor, "Γεια");

        Assert.Equal(HttpStatusCode.Forbidden, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, r2.StatusCode);
        Assert.Equal(0, h.Db(db => db.Set<Comment>().Count(c => c.PostId == postByAuthor)));
    }

    [Fact]
    public async Task A_viewer_does_not_see_comments_of_users_they_blocked()
    {
        var postId = e.NewPost(11);
        var viewerId = e.Athlete(52);
        var blockedId = e.Athlete(53);
        var visibleId = e.Athlete(54);
        var blockedComment = e.AddComment(postId, $"athlete:{blockedId}", "Σχόλιο του αποκλεισμένου");
        var visibleComment = e.AddComment(postId, $"athlete:{visibleId}", "Σχόλιο που φαίνεται", DateTimeOffset.UtcNow.AddSeconds(1));
        h.Db(db => { db.Set<Block>().Add(new Block { BlockerKey = $"athlete:{viewerId}", BlockedKey = $"athlete:{blockedId}", CreatedAt = DateTimeOffset.UtcNow, IsDemo = true }); db.SaveChanges(); return 0; });

        var viewerCard = await e.Card(await h.LoginAsync("athlete", viewerId), postId);
        var anonCard = await e.Card(app.Client(), postId);

        Assert.DoesNotContain(blockedComment, EngageHarness.CommentIds(viewerCard));
        Assert.Contains(visibleComment, EngageHarness.CommentIds(viewerCard));
        Assert.Contains(blockedComment, EngageHarness.CommentIds(anonCard));
        Assert.Contains("data-comment-count=\"1\"", viewerCard);
    }

    // ---- comments: delete / hide / report ----

    [Fact]
    public async Task Only_the_comment_author_can_delete_a_comment()
    {
        var postId = e.NewPost(12);
        var authorId = e.Athlete(55);
        var commentId = e.AddComment(postId, $"athlete:{authorId}", "Θα το σβήσω");

        var other = await h.PostAsync(await h.LoginAsync("athlete", e.Athlete(56)), "/feed/comment/delete", new() { ["commentId"] = commentId.ToString(), ["returnUrl"] = "/feed" });
        var postAuthor = await h.PostAsync(await e.AuthorOf(postId), "/feed/comment/delete", new() { ["commentId"] = commentId.ToString(), ["returnUrl"] = "/feed" });
        var anon = await h.PostAsync(app.Client(), "/feed/comment/delete", new() { ["commentId"] = commentId.ToString() }, tokenPage: "/demo");

        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postAuthor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, anon.StatusCode);
        Assert.Equal(1, h.Db(db => db.Set<Comment>().Count(c => c.Id == commentId)));

        var own = await h.PostAsync(await h.LoginAsync("athlete", authorId), "/feed/comment/delete", new() { ["commentId"] = commentId.ToString(), ["returnUrl"] = "/feed" });

        Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
        Assert.Equal(0, h.Db(db => db.Set<Comment>().Count(c => c.Id == commentId)));
    }

    [Fact]
    public async Task Only_the_post_author_can_hide_a_comment_and_it_vanishes_for_everyone()
    {
        var postId = e.NewPost(13);
        var authorId = e.Athlete(57);
        var commentId = e.AddComment(postId, $"athlete:{authorId}", "Ενοχλητικό σχόλιο");
        var form = new Func<Dictionary<string, string>>(() => new() { ["commentId"] = commentId.ToString(), ["returnUrl"] = "/feed" });

        var other = await h.PostAsync(await h.LoginAsync("athlete", e.Athlete(58)), "/feed/comment/hide", form());
        var commenter = await h.PostAsync(await h.LoginAsync("athlete", authorId), "/feed/comment/hide", form());
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, commenter.StatusCode);
        Assert.False(h.Db(db => db.Set<Comment>().Where(c => c.Id == commentId).Select(c => c.IsHidden).First()));

        var hide = await h.PostAsync(await e.AuthorOf(postId), "/feed/comment/hide", form());

        Assert.Equal(HttpStatusCode.Redirect, hide.StatusCode);
        Assert.True(h.Db(db => db.Set<Comment>().Where(c => c.Id == commentId).Select(c => c.IsHidden).First()));
        var anonCard = await e.Card(app.Client(), postId);
        Assert.DoesNotContain(commentId, EngageHarness.CommentIds(anonCard));
        Assert.Contains("data-comment-count=\"0\"", anonCard);
    }

    [Fact]
    public async Task Reporting_hides_for_the_reporter_at_once_and_three_distinct_reports_hide_for_everyone()
    {
        var postId = e.NewPost(14);
        var commentId = e.AddComment(postId, $"athlete:{e.Athlete(59)}", "Σχόλιο προς αναφορά");
        async Task<HttpResponseMessage> Report(int athleteId, string reason = "spam") =>
            await h.PostAsync(await h.LoginAsync("athlete", athleteId), "/feed/comment/report", new() { ["commentId"] = commentId.ToString(), ["reason"] = reason, ["returnUrl"] = "/feed" });
        var r1 = e.Athlete(60);
        var r2 = e.Athlete(61);
        var r3 = e.Athlete(62);

        var invalid = await Report(r1, "nonsense");
        Assert.Equal(0, h.Db(db => db.Set<CommentReport>().Count(r => r.CommentId == commentId)));
        Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);

        await Report(r1);
        await Report(r1); // same reporter twice counts once
        Assert.DoesNotContain(commentId, EngageHarness.CommentIds(await e.Card(await h.LoginAsync("athlete", r1), postId)));
        Assert.Contains(commentId, EngageHarness.CommentIds(await e.Card(await h.LoginAsync("athlete", r2), postId)));
        Assert.Contains(commentId, EngageHarness.CommentIds(await e.Card(app.Client(), postId)));
        Assert.Equal(1, h.Db(db => db.Set<CommentReport>().Count(r => r.CommentId == commentId)));

        await Report(r2);
        Assert.Contains(commentId, EngageHarness.CommentIds(await e.Card(app.Client(), postId))); // 2 of 3
        await Report(r3);

        Assert.DoesNotContain(commentId, EngageHarness.CommentIds(await e.Card(app.Client(), postId)));
        Assert.True(h.Db(db => db.Set<Comment>().Where(c => c.Id == commentId).Select(c => c.IsHidden).First()));
    }

    [Fact]
    public async Task You_cannot_report_your_own_comment_and_anonymous_cannot_report()
    {
        var postId = e.NewPost(15);
        var id = e.Athlete(63);
        var commentId = e.AddComment(postId, $"athlete:{id}", "Δικό μου");

        var own = await h.PostAsync(await h.LoginAsync("athlete", id), "/feed/comment/report", new() { ["commentId"] = commentId.ToString(), ["reason"] = "spam" });
        var anon = await h.PostAsync(app.Client(), "/feed/comment/report", new() { ["commentId"] = commentId.ToString(), ["reason"] = "spam" }, tokenPage: "/demo");

        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, anon.StatusCode);
    }

    // ---- comments: list ----

    [Fact]
    public async Task Collapsed_list_shows_the_2_latest_oldest_first_and_expands_with_the_query_link()
    {
        var postId = e.NewPost(16);
        var author = $"athlete:{e.Athlete(64)}";
        var ids = Enumerable.Range(1, 5).Select(i => e.AddComment(postId, author, $"Σχόλιο νούμερο {i}", DateTimeOffset.UtcNow.AddMinutes(-10 + i))).ToList();
        var client = app.Client();

        var collapsed = await e.Card(client, postId);

        Assert.Equal(ids.Skip(3), EngageHarness.CommentIds(collapsed));
        Assert.Contains("data-comment-count=\"5\"", collapsed);
        var link = Regex.Match(collapsed, "<a[^>]*href=\"([^\"]*comments=" + postId + "[^\"]*)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.True(link.Success, "expand link missing");
        Assert.Contains("(5)", EngageHarness.Text(link.Groups[2].Value));
        Assert.Contains($"#post-{postId}", WebUtility.HtmlDecode(link.Groups[1].Value));

        var expanded = await e.Card(client, postId, WebUtility.HtmlDecode(link.Groups[1].Value).Split('#')[0]);

        Assert.Equal(ids, EngageHarness.CommentIds(expanded));
    }

    [Fact]
    public async Task Post_cards_carry_the_post_anchor_id()
    {
        var postId = e.NewPost(17);

        var html = await FeedHarness.Html(app.Client(), EngageHarness.TagUrl(postId));

        Assert.Matches($"<article[^>]*id=\"post-{postId}\"", html);
    }

    [Fact]
    public async Task Composer_is_for_signed_in_only_and_anonymous_gets_a_sign_in_hint()
    {
        var postId = e.NewPost(18);

        var anon = await e.Card(app.Client(), postId);
        var signed = await e.Card(await h.LoginAsync("athlete", e.Athlete(65)), postId);

        Assert.DoesNotContain("action=\"/feed/comment\"", anon);
        Assert.Contains("href=\"/demo", anon);
        Assert.Contains("action=\"/feed/comment\"", signed);
        Assert.Matches("<textarea[^>]*maxlength=\"280\"", signed);
    }

    [Fact]
    public async Task Comment_actions_are_offered_by_role()
    {
        var postId = e.NewPost(19);
        var authorId = e.Athlete(66);
        e.AddComment(postId, $"athlete:{authorId}", "Σχόλιο");

        var own = await e.Card(await h.LoginAsync("athlete", authorId), postId);
        var other = await e.Card(await h.LoginAsync("athlete", e.Athlete(67)), postId);
        var postAuthor = await e.Card(await e.AuthorOf(postId), postId);

        Assert.Contains("action=\"/feed/comment/delete\"", own);
        Assert.DoesNotContain("action=\"/feed/comment/report\"", own);
        Assert.Contains("action=\"/feed/comment/report\"", other);
        Assert.DoesNotContain("action=\"/feed/comment/delete\"", other);
        Assert.DoesNotContain("action=\"/feed/comment/hide\"", other);
        Assert.Contains("action=\"/feed/comment/hide\"", postAuthor);
    }

    // ---- public engagement ----

    [Fact]
    public async Task Kudos_count_is_public_for_anonymous_other_and_author()
    {
        var (postId, authorKey, count) = h.Db(db =>
        {
            var row = db.Set<Kudos>().GroupBy(k => k.PostId).Select(g => new { PostId = g.Key, Count = g.Count() }).OrderBy(x => x.PostId).Skip(3).First();
            return (row.PostId, db.Set<Post>().Where(p => p.Id == row.PostId).Select(p => p.AuthorKey).First(), row.Count);
        });
        Assert.True(FeedKeys.TryParse(authorKey, out var kind, out var authorId));
        var author = await h.LoginAsync(kind == AuthorKind.Athlete ? "athlete" : "coach", kind == AuthorKind.Club ? h.Db(db => db.Coaches.First(c => c.ClubId == authorId && c.Role == CoachRole.Head).Id) : authorId);

        var anon = await e.Card(app.Client(), postId);
        var other = await e.Card(await h.LoginAsync("coach", h.CoachId(3)), postId);
        var own = await e.Card(author, postId);

        foreach (var html in new[] { anon, other, own })
            Assert.Contains($"data-kudos-count=\"{count}\"", html);
    }

    [Fact]
    public async Task Likers_line_names_the_most_recent_liker_and_counts_the_rest()
    {
        var postId = e.NewPost(0);
        var (a1, a2, a3, viewerId) = (e.Athlete(80), e.Athlete(81), e.Athlete(82), e.Athlete(83));
        var now = DateTimeOffset.UtcNow;
        h.Db(db =>
        {
            db.Set<Kudos>().AddRange(
                new Kudos { PostId = postId, ActorKey = $"athlete:{a1}", CreatedAt = FeedKeys.Truncate(now.AddHours(-3)), IsDemo = true },
                new Kudos { PostId = postId, ActorKey = $"athlete:{a3}", CreatedAt = FeedKeys.Truncate(now.AddHours(-2)), IsDemo = true },
                new Kudos { PostId = postId, ActorKey = $"athlete:{a2}", CreatedAt = FeedKeys.Truncate(now.AddHours(-1)), IsDemo = true });
            db.SaveChanges();
            return 0;
        });
        var (name2, slug2, name3) = h.Db(db => (EngageHarness.Name(db.Athletes.First(a => a.Id == a2)), db.Athletes.First(a => a.Id == a2).Slug, EngageHarness.Name(db.Athletes.First(a => a.Id == a3))));

        var anon = await e.Card(app.Client(), postId);
        Assert.Equal($"{name2} και 2 ακόμη", Line(anon));
        Assert.Contains("data-kudos-count=\"3\"", anon);
        Assert.Matches($"<a[^>]*href=\"/p/{slug2}\"", Regex.Match(anon, "<p[^>]*data-likers-line[^>]*>.*?</p>", RegexOptions.Singleline).Value);

        var viewer = await h.LoginAsync("athlete", viewerId);
        await h.PostAsync(viewer, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "1", ["returnUrl"] = "/feed" });
        var liked = await e.Card(viewer, postId);
        Assert.Equal($"Σας αρέσει μαζί με {name2} και 2 ακόμη", Line(liked));
        Assert.Contains("data-kudos-count=\"4\"", liked);

        h.Db(db => db.Set<Kudos>().Where(k => k.PostId == postId && k.ActorKey != $"athlete:{viewerId}").ExecuteDelete());
        Assert.Equal("Σας αρέσει", Line(await e.Card(viewer, postId)));
        h.Db(db => { db.Set<Kudos>().Add(new Kudos { PostId = postId, ActorKey = $"athlete:{a1}", CreatedAt = FeedKeys.Truncate(now.AddHours(-3)), IsDemo = true }); db.SaveChanges(); return 0; });
        Assert.Equal($"Σας αρέσει μαζί με {EngageHarness.Name(h.Db(db => db.Athletes.First(a => a.Id == a1)))}", Line(await e.Card(viewer, postId)));
        Assert.Equal($"{EngageHarness.Name(h.Db(db => db.Athletes.First(a => a.Id == viewerId)))} και 1 ακόμη", Line(await e.Card(app.Client(), postId))); // anonymous: the viewer is just the newest liker
        Assert.NotEqual(name3, name2);

        static string Line(string card) => EngageHarness.Text(Regex.Match(card, "<p[^>]*data-likers-line[^>]*>(.*?)</p>", RegexOptions.Singleline).Groups[1].Value);
    }

    [Fact]
    public async Task Post_without_kudos_has_no_likers_line_and_shows_zero()
    {
        var postId = e.NewPost(1);

        var card = await e.Card(app.Client(), postId);

        Assert.DoesNotContain("data-likers-line", card);
        Assert.Contains("data-kudos-count=\"0\"", card);
    }

    [Fact]
    public async Task Kudos_toggle_returns_to_the_same_post_anchor_and_the_form_carries_it()
    {
        var postId = e.NewPost(2);
        var athlete = await h.LoginAsync("athlete", e.Athlete(84));

        var card = await e.Card(athlete, postId);
        var kudosForm = Regex.Match(card, "<form[^>]*action=\"/feed/kudos\".*?</form>", RegexOptions.Singleline).Value;
        Assert.Matches($"value=\"[^\"]*#post-{postId}\"", kudosForm);

        var response = await h.PostAsync(athlete, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "1", ["returnUrl"] = $"/feed#post-{postId}" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith($"#post-{postId}", response.Headers.Location!.OriginalString);
        Assert.Contains("status=kudos", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Stylesheet_has_the_burst_animation_off_under_reduced_motion_and_the_desktop_breakpoint()
    {
        var html = await FeedHarness.Html(app.Client(), "/feed");
        var href = Regex.Match(html, "href=\"([^\"]*bp-engage[^\"]*\\.css)\"").Groups[1].Value;
        Assert.NotEmpty(href);

        var css = await app.Client().GetStringAsync(href);

        Assert.Contains("@keyframes bp-engage-burst", css);
        Assert.Matches(@"@media \(prefers-reduced-motion: reduce\)[^{]*\{[^@]*animation:\s*none", css);
        Assert.Contains("min-width: 1100px", css);
    }

    // ---- who to follow ----

    [Fact]
    public async Task Who_to_follow_has_3_to_5_people_never_self_followed_or_blocked_and_prefers_the_same_region()
    {
        var viewerId = e.Athlete(90);
        var viewerKey = $"athlete:{viewerId}";
        var region = h.Db(db => db.Athletes.Where(a => a.Id == viewerId).Select(a => a.Region).First());
        var followed = h.Db(db => db.Athletes.Where(a => a.IsDemo && a.Id != viewerId && a.Region == region).OrderBy(a => a.Id).Take(3).Select(a => $"athlete:{a.Id}").ToList());
        var blocked = h.Db(db => db.Athletes.Where(a => a.IsDemo && a.Id != viewerId && a.Region == region).OrderBy(a => a.Id).Skip(3).Take(1).Select(a => $"athlete:{a.Id}").Single());
        h.Db(db =>
        {
            db.Set<Follow>().Where(f => f.FollowerKey == viewerKey).ExecuteDelete();
            db.Set<Follow>().AddRange(followed.Select(k => new Follow { FollowerKey = viewerKey, TargetKey = k, CreatedAt = DateTimeOffset.UtcNow, IsDemo = true }));
            db.Set<Block>().Add(new Block { BlockerKey = viewerKey, BlockedKey = blocked, CreatedAt = DateTimeOffset.UtcNow, IsDemo = true });
            db.SaveChanges();
            return 0;
        });
        var viewer = await h.LoginAsync("athlete", viewerId);

        var html = await FeedHarness.Html(viewer, "/feed");

        var section = Regex.Match(html, "data-engage=\"follow\".*?</section>", RegexOptions.Singleline).Value;
        var rows = Regex.Matches(section, "data-follow-key=\"([^\"]+)\"[^>]*data-region=\"([^\"]*)\"").Select(m => (Key: m.Groups[1].Value, Region: m.Groups[2].Value)).DistinctBy(r => r.Key).ToList();
        Assert.InRange(rows.Count, 3, 5);
        Assert.DoesNotContain(rows, r => r.Key == viewerKey);
        Assert.DoesNotContain(rows, r => followed.Contains(r.Key));
        Assert.DoesNotContain(rows, r => r.Key == blocked);
        Assert.Contains(rows, r => r.Region == region);
        Assert.Contains("action=\"/feed/follow\"", section);
        Assert.Matches("name=\"targetKey\"[^>]*value=\"(athlete|coach):\\d+\"", section);
    }

    [Fact]
    public async Task Who_to_follow_for_a_coach_and_for_anonymous_shows_people_but_anonymous_gets_no_follow_forms()
    {
        var coach = await FeedHarness.Html(await h.LoginAsync("coach", h.CoachId(7)), "/feed");
        var anon = await FeedHarness.Html(app.Client(), "/feed");

        var coachSection = Regex.Match(coach, "data-engage=\"follow\".*?</section>", RegexOptions.Singleline).Value;
        var anonSection = Regex.Match(anon, "data-engage=\"follow\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains($"data-follow-key=", coachSection);
        Assert.Contains("action=\"/feed/follow\"", coachSection);
        Assert.DoesNotContain("data-follow-key=\"coach:" + h.CoachId(7) + "\"", coachSection);
        Assert.Contains("data-follow-key=", anonSection);
        Assert.DoesNotContain("<form", anonSection);
    }

    [Fact]
    public async Task Following_from_the_suggestions_uses_the_existing_endpoint()
    {
        var viewerId = e.Athlete(91);
        var viewerKey = $"athlete:{viewerId}";
        h.Db(db => db.Set<Follow>().Where(f => f.FollowerKey == viewerKey).ExecuteDelete());
        var viewer = await h.LoginAsync("athlete", viewerId);
        var key = Regex.Match(await FeedHarness.Html(viewer, "/feed"), "data-follow-key=\"([^\"]+)\"").Groups[1].Value;

        var response = await h.PostAsync(viewer, "/feed/follow", new() { ["targetKey"] = key, ["returnUrl"] = "/feed" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, h.Db(db => db.Set<Follow>().Count(f => f.FollowerKey == viewerKey && f.TargetKey == key)));
        var after = await FeedHarness.Html(viewer, "/feed");
        Assert.DoesNotContain($"data-follow-key=\"{key}\"", after);
    }

    // ---- new posts pill ----

    private HttpClient WithCookie(string value)
    {
        var client = app.Client();
        client.DefaultRequestHeaders.Add("Cookie", $"bp_lastvisit={value}");
        return client;
    }

    [Fact]
    public async Task Pill_counts_posts_newer_than_the_last_visit_cookie()
    {
        var since = DateTimeOffset.UtcNow.AddDays(-3);
        var expected = h.Db(db => db.Set<Post>().Count(p => !p.IsHidden && p.CreatedAt > since.AddTicks(-since.Ticks % TimeSpan.TicksPerSecond)));

        var html = await FeedHarness.Html(WithCookie(since.ToUnixTimeSeconds().ToString()), "/feed");

        Assert.Equal(expected, int.Parse(Regex.Match(html, "data-new-count=\"(\\d+)\"").Groups[1].Value));
        Assert.Contains("νέες αναρτήσεις", EngageHarness.Text(Regex.Match(html, "data-engage=\"new-posts\".*?</a>", RegexOptions.Singleline).Value));
    }

    [Fact]
    public async Task Fresh_session_defaults_to_the_last_24_hours_and_sets_the_cookie()
    {
        var expected = h.Db(db => db.Set<Post>().Count(p => !p.IsHidden && p.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24)));
        var client = app.Client();

        var response = await client.GetAsync("/feed");
        var html = await response.Content.ReadAsStringAsync();

        Assert.InRange(expected, 3, 100);
        Assert.Equal(expected, int.Parse(Regex.Match(html, "data-new-count=\"(\\d+)\"").Groups[1].Value));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bp_lastvisit="));
        var second = await FeedHarness.Html(client, "/feed"); // the cookie now says "just now"
        Assert.DoesNotContain("data-new-count", second);
    }

    [Fact]
    public async Task Pill_is_absent_for_a_future_cookie_a_garbage_cookie_value_and_later_pages()
    {
        var future = await FeedHarness.Html(WithCookie(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()), "/feed");
        var garbage = await app.Client().GetAsync("/feed");
        Assert.DoesNotContain("data-new-count", future);
        Assert.Equal(HttpStatusCode.OK, garbage.StatusCode);

        var oldCookie = WithCookie(DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeSeconds().ToString());
        var first = await FeedHarness.Html(oldCookie, "/feed");
        var more = Regex.Match(first, "<a[^>]*data-feed-more[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(more);
        Assert.DoesNotContain("data-new-count", await FeedHarness.Html(WithCookie(DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeSeconds().ToString()), more));
        Assert.Equal(HttpStatusCode.OK, (await WithCookie("not-a-number").GetAsync("/feed")).StatusCode);
    }

    // ---- layout seams ----

    [Fact]
    public async Task Feed_has_the_sidebar_the_inline_boxes_after_the_third_post_and_no_seeded_label()
    {
        var html = await FeedHarness.Html(app.Client(), "/feed");

        Assert.Contains("data-engage=\"sidebar\"", html);
        Assert.Contains("data-engage=\"trends\"", html);
        Assert.Contains("data-engage=\"follow\"", html);
        Assert.DoesNotContain("data-engage=\"seeded\"", html);   // flow-release O1
        Assert.DoesNotMatch("(?i)seeded|Δείγμα δραστηριότητας", html);
        var inline = html.IndexOf("data-engage=\"inline\"", StringComparison.Ordinal);
        Assert.True(inline > 0, "inline boxes missing");
        var closings = Regex.Matches(html, "</article>").Select(m => m.Index).ToList();
        Assert.True(inline > closings[2] && inline < closings[3], "inline boxes must sit between the 3rd and 4th post");
    }

    [Fact]
    public async Task Right_rail_shows_four_athletes_looking_for_a_team_then_at_most_five_trends()
    {
        var html = await FeedHarness.Html(app.Client(), "/feed");

        var rail = Regex.Match(html, "<aside[^>]*data-engage=\"sidebar\"[^>]*>.*?</aside>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(rail);
        var looking = Regex.Match(rail, "<section[^>]*data-engage=\"looking\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains("Ψάχνουν ομάδα τώρα", looking);
        var slugs = Regex.Matches(looking, "href=\"/p/([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Equal(4, slugs.Count);
        Assert.All(slugs, s => Assert.True(h.Db(db => db.Athletes.Single(a => a.Slug == s).LookingForTeam), $"{s} is not looking for a team"));
        Assert.Matches("<svg[^>]*class=\"[^\"]*bp-jersey", looking);
        Assert.DoesNotContain("data-engage=\"follow\"", rail);   // who-to-follow leaves the rail (phones keep it inline)
        var trends = Regex.Match(rail, "data-engage=\"trends\".*?</section>", RegexOptions.Singleline).Value;
        Assert.InRange(Regex.Matches(trends, "data-tag-key=").Count, 1, 5);
        Assert.True(rail.IndexOf("data-engage=\"looking\"", StringComparison.Ordinal) < rail.IndexOf("data-engage=\"trends\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Other_pages_are_not_affected()
    {
        var client = await h.LoginAsync("coach", h.CoachId(8));
        var slug = Regex.Match(await FeedHarness.Html(client, "/search"), "href=\"/p/([^\"/]+)\"").Groups[1].Value;
        Assert.NotEmpty(slug);

        foreach (var path in new[] { "/", "/search", $"/p/{slug}", "/feed?tab=following" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.DoesNotContain("data-engage", await FeedHarness.Html(app.Client(), "/"));
    }

    // ---- seed ----

    private static readonly DateTimeOffset Ref = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static (DemoDataSet Data, FeedSeedSet Feed, EngageSeedSet Engage) Seed(DateTimeOffset? reference = null)
    {
        var data = DemoDataGenerator.Generate(42);
        var n = 0;
        foreach (var c in data.Clubs) c.Id = ++n;
        n = 0;
        foreach (var c in data.Coaches) c.Id = ++n;
        n = 0;
        foreach (var a in data.Athletes) a.Id = ++n;
        var feed = FeedSeeder.Generate(data, reference ?? Ref);
        return (data, feed, EngageSeeder.Generate(data, feed.Posts, feed.Kudos, reference ?? Ref));
    }

    [Fact]
    public void Seed_has_150_comments_by_adult_athletes_and_coaches_on_other_peoples_posts()
    {
        var (data, feed, seed) = Seed();
        var athletes = data.Athletes.ToDictionary(a => $"athlete:{a.Id}");

        Assert.Equal(150, seed.Comments.Count);
        Assert.All(seed.Comments, c =>
        {
            Assert.NotEqual(c.Post!.AuthorKey, c.AuthorKey);
            Assert.InRange(c.Text.Length, 5, 280);
            Assert.DoesNotContain("http", c.Text);
            Assert.DoesNotContain("www.", c.Text);
            Assert.InRange(c.CreatedAt, c.Post.CreatedAt, Ref);
            Assert.True(c.IsDemo);
            if (athletes.TryGetValue(c.AuthorKey, out var a)) Assert.True(Safeguards.IsAdult(a.BirthYear, Ref));
        });
        Assert.Contains(seed.Comments, c => c.AuthorKey.StartsWith("coach:"));
        Assert.Contains(seed.Comments, c => c.AuthorKey.StartsWith("athlete:"));
        Assert.InRange(seed.Comments.Select(c => c.AuthorKey).Distinct().Count(), 25, 200);
        Assert.InRange(seed.Comments.Select(c => c.Post).Distinct().Count(), 20, 40);
        Assert.Equal(seed.Comments.Count, seed.Comments.Select(c => (c.Post, c.AuthorKey, c.Text)).Distinct().Count());
    }

    [Fact]
    public void Seed_raises_kudos_to_about_250_with_some_posts_at_20_to_40()
    {
        var (_, feed, seed) = Seed();

        var all = feed.Kudos.Concat(seed.ExtraKudos).ToList();
        Assert.InRange(all.Count, 230, 270);
        var perPost = all.GroupBy(k => k.Post!).Select(g => g.Count()).OrderByDescending(n => n).ToList();
        Assert.InRange(perPost[0], 20, 40);
        Assert.True(perPost.Count(n => n >= 20) >= 3, "at least 3 posts with 20+ kudos");
        Assert.True(perPost.Count(n => n >= 20) <= 8, "not every post is viral");
        Assert.Equal(all.Count, all.Select(k => (k.Post, k.ActorKey)).Distinct().Count());
        Assert.DoesNotContain(all, k => k.ActorKey == k.Post!.AuthorKey);
        Assert.All(seed.ExtraKudos, k => { Assert.InRange(k.CreatedAt, k.Post!.CreatedAt, Ref); Assert.True(k.IsDemo); });
    }

    [Fact]
    public void Seed_puts_hashtags_on_about_40_percent_of_posts_with_at_most_3_each()
    {
        var (_, feed, seed) = Seed();

        var tagged = seed.Tags.Select(t => t.Post!).Distinct().ToList();
        Assert.InRange(tagged.Count, 12, 20); // 30% - 50% of 40
        Assert.All(seed.Tags.GroupBy(t => t.Post!), g => Assert.InRange(g.Count(), 1, 3));
        var recent = seed.Tags.Where(t => Ref - t.Post!.CreatedAt <= TimeSpan.FromDays(7)).Select(t => t.Tag).Distinct().ToList();
        Assert.True(recent.Count >= 3, "trends need at least 3 distinct tags in the last 7 days");
        Assert.Contains(seed.Tags, t => t.Tag == "ψαχνωομαδα");
        Assert.Contains(seed.Tags, t => t.Tag == "tryouts");
        Assert.All(seed.Tags, t => Assert.Equal(PostTagParser.Fold(t.Label), t.Tag));
        // Where the post has text, the tags are exactly the hashtags parsed from it.
        foreach (var (post, text) in seed.TextEdits)
        {
            Assert.InRange(text.Length, 1, 280);
            var expected = PostTagParser.Parse(text, post.Title).Select(t => t.Tag).ToHashSet();
            Assert.Subset(seed.Tags.Where(t => t.Post == post).Select(t => t.Tag).ToHashSet(), expected);
        }
    }

    [Fact]
    public void Seed_is_deterministic_and_shifts_with_the_reference_time()
    {
        var a = Seed();
        var b = Seed();
        var c = Seed(Ref.AddDays(10));

        static string Sig(EngageSeedSet s) => string.Join("|", s.Comments.Select(x => $"{x.Post!.AuthorKey}/{x.AuthorKey}/{x.Text}/{x.CreatedAt.Ticks}"))
            + "#" + string.Join("|", s.ExtraKudos.Select(x => $"{x.Post!.AuthorKey}/{x.ActorKey}/{x.CreatedAt.Ticks}"))
            + "#" + string.Join("|", s.Tags.Select(x => $"{x.Post!.AuthorKey}/{x.Tag}"));
        Assert.Equal(Sig(a.Engage), Sig(b.Engage));
        Assert.Equal(a.Engage.Comments.Select(x => x.CreatedAt.AddDays(10)), c.Engage.Comments.Select(x => x.CreatedAt));
    }

    [Fact]
    public void Reset_persists_comments_kudos_and_tags_and_the_feed_seed_is_untouched()
    {
        Assert.True(h.Db(db => db.Set<Comment>().Count(c => c.IsDemo)) >= 150);
        Assert.InRange(h.Db(db => db.Set<Kudos>().Count()), 230, 400);
        Assert.True(h.Db(db => db.Set<PostTag>().Count()) >= 12);
    }
}

/// <summary>A separate database and app, so engage services that throw on every call are tested without disturbing the shared one.</summary>
public class ThrowingEngageFactory : BallpassAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Ballpass", TestDb.ConnectionString("ballpass_engagethrow"));
        builder.ConfigureServices(services => services.AddScoped<IEngageService, ThrowingEngageService>());
    }
}

public class ThrowingEngageService : IEngageService
{
    private static T Boom<T>() => throw new InvalidOperationException("engage is down (test)");
    public Task<IReadOnlyDictionary<int, PostEngagement>> GetEngagementAsync(FeedViewer viewer, IReadOnlyList<int> postIds, int? expandedPostId, CancellationToken ct = default) => Boom<Task<IReadOnlyDictionary<int, PostEngagement>>>();
    public Task<CommentOutcome> AddCommentAsync(FeedViewer viewer, int postId, string? text, CancellationToken ct = default) => Boom<Task<CommentOutcome>>();
    public Task<ModOutcome> DeleteCommentAsync(FeedViewer viewer, int commentId, CancellationToken ct = default) => Boom<Task<ModOutcome>>();
    public Task<ModOutcome> HideCommentAsync(FeedViewer viewer, int commentId, CancellationToken ct = default) => Boom<Task<ModOutcome>>();
    public Task<ModOutcome> ReportCommentAsync(FeedViewer viewer, int commentId, string? reason, CancellationToken ct = default) => Boom<Task<ModOutcome>>();
    public Task<IReadOnlyList<TrendingTag>> TrendingAsync(CancellationToken ct = default) => Boom<Task<IReadOnlyList<TrendingTag>>>();
    public Task<IReadOnlyList<Suggestion>> SuggestionsAsync(FeedViewer viewer, int take, CancellationToken ct = default) => Boom<Task<IReadOnlyList<Suggestion>>>();
    public Task<int> NewPostsCountAsync(FeedViewer viewer, DateTimeOffset since, CancellationToken ct = default) => Boom<Task<int>>();
}

public class EngageFailureTests(ThrowingEngageFactory factory) : IClassFixture<ThrowingEngageFactory>
{
    private async Task<HttpClient> CoachAsync()
    {
        var client = factory.Client();
        var page = await FeedHarness.Html(client, "/demo");
        var id = Regex.Match(page, "data-persona=\"coach\"[^>]*>\\s*<input[^>]*name=\"personaType\"[^>]*>\\s*<input[^>]*name=\"personaId\"[^>]*value=\"(\\d+)\"").Groups[1].Value;
        var r = await client.PostAsync("/demo/login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["personaType"] = "coach", ["personaId"] = id, ["returnUrl"] = "", ["__RequestVerificationToken"] = FeedHarness.Token(page) }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return client;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/search")]
    [InlineData("/feed")]
    [InlineData("/feed?tab=following")]
    public async Task Pages_still_render_when_the_engage_services_throw(string path)
    {
        var response = await (await CoachAsync()).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Feed_still_lists_posts_and_the_profile_works_when_engage_throws()
    {
        var client = await CoachAsync();
        var html = await FeedHarness.Html(client, "/feed");

        Assert.NotEmpty(FeedHarness.Cards(html));
        var slug = Regex.Match(html, "href=\"/p/([^\"/?#]+)\"").Groups[1].Value;
        Assert.NotEmpty(slug);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/p/{slug}")).StatusCode);
    }
}
