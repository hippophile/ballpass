using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Loop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/messages/specs/messages/spec.md
[Collection("db")]
public class MessagesTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    // ---------- helpers ----------

    private static Task<string> Html(HttpClient c, string path) => FeedHarness.Html(c, path);

    private FeaturedOptions Featured => app.Services.GetRequiredService<IOptions<DemoOptions>>().Value.Featured;

    /// <summary>Interests (and through the cascade their conversations) created by a test and removed again.</summary>
    private sealed class Chat(BallpassAppFactory app) : IDisposable
    {
        private readonly List<int> ids = [];

        private T Db<T>(Func<BallpassDbContext, T> f)
        {
            using var scope = app.Services.CreateScope();
            return f(scope.ServiceProvider.GetRequiredService<BallpassDbContext>());
        }

        /// <summary>A women's-club head coach without any conversation (skip-th).</summary>
        public int FreeCoach(int skip) => Db(db => db.Coaches
            .Where(c => c.IsDemo && c.Role == CoachRole.Head && c.Club!.Gender == Gender.Female && !db.Set<Conversation>().Any(x => x.CoachId == c.Id))
            .OrderBy(c => c.Id).Skip(skip).Select(c => c.Id).First());

        /// <summary>An interest from the coach to a fresh athlete (no interest, no conversation at all).</summary>
        public (int InterestId, int CoachId, int AthleteId, string Slug) Interest(int coachId, InterestReply reply, string? note = null, int skip = 0) => Db(db =>
        {
            var gender = db.Coaches.Where(c => c.Id == coachId).Select(c => c.Club!.Gender).Single();
            var athlete = db.Athletes.Where(a => a.IsDemo && a.Gender == gender && a.Stats.Any()
                    && !db.Interests.Any(i => i.AthleteId == a.Id) && !db.Set<Conversation>().Any(x => x.AthleteId == a.Id))
                .OrderBy(a => a.Id).Skip(skip).First();
            var now = DateTimeOffset.UtcNow;
            var interest = new Interest
            {
                CoachId = coachId, AthleteId = athlete.Id, Note = note, CreatedAt = now.AddDays(-3), SeenAt = reply == InterestReply.None ? null : now.AddDays(-2),
                Reply = reply, RepliedAt = reply == InterestReply.None ? null : now.AddDays(-2).AddHours(1), IsDemo = true,
            };
            db.Interests.Add(interest);
            db.SaveChanges();
            ids.Add(interest.Id);
            return (interest.Id, coachId, athlete.Id, athlete.Slug);
        });

        /// <summary>A conversation for an accepted interest with the given messages.</summary>
        public int Conversation(int interestId, params (MessageSender Sender, string Body, DateTimeOffset SentAt, DateTimeOffset? ReadAt)[] messages) => Db(db =>
        {
            var i = db.Interests.Single(x => x.Id == interestId);
            var c = new Conversation { InterestId = i.Id, CoachId = i.CoachId, AthleteId = i.AthleteId, CreatedAt = i.RepliedAt ?? DateTimeOffset.UtcNow, IsDemo = true };
            foreach (var m in messages)
                c.Messages.Add(new Message { SenderKind = m.Sender, Body = m.Body, SentAt = m.SentAt, ReadAt = m.ReadAt, IsDemo = true });
            db.Add(c);
            db.SaveChanges();
            return c.Id;
        });

        /// <summary>Accepted interest + conversation with one read coach message and one unread athlete message.</summary>
        public (int ConversationId, int CoachId, int AthleteId, string Slug) Connected(int coachSkip = 0, int athleteSkip = 0)
        {
            var coach = FreeCoach(coachSkip);
            var i = Interest(coach, InterestReply.Accepted, skip: athleteSkip);
            var now = DateTimeOffset.UtcNow;
            var id = Conversation(i.InterestId,
                (MessageSender.Coach, "Καλησπέρα, θα σε δούμε την Τρίτη;", now.AddHours(-2), now.AddHours(-1)),
                (MessageSender.Athlete, "Ναι, θα είμαι εκεί!", now.AddMinutes(-30), null));
            return (id, coach, i.AthleteId, i.Slug);
        }

        public void Dispose() => Db(db => db.Interests.Where(i => ids.Contains(i.Id)).ExecuteDelete());
    }

    private List<Message> Messages(int conversationId) =>
        h.Db(db => db.Set<Message>().AsNoTracking().Where(m => m.ConversationId == conversationId).OrderBy(m => m.SentAt).ThenBy(m => m.Id).ToList());

    private static int NavUnread(string html)
    {
        var m = Regex.Match(html, "<a[^>]*href=\"/messages\"[^>]*data-unread=\"(\\d+)\"");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static string Thread(string html) =>
        Regex.Match(html, "<section[^>]*class=\"bp-msg-thread\".*?</section>", RegexOptions.Singleline).Value;

    private static List<string> Bubbles(string html) =>
        Regex.Matches(html, "<li[^>]*class=\"bp-msg-bubble[^\"]*\"[^>]*>.*?</li>", RegexOptions.Singleline).Select(m => m.Value).ToList();

    // ---------- access ----------

    [Fact]
    public async Task Only_the_two_participants_can_read_a_thread()
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var otherCoach = chat.FreeCoach(1);
        var other = chat.Interest(otherCoach, InterestReply.Accepted, skip: 1);

        Assert.Equal(HttpStatusCode.OK, (await (await h.LoginAsync("coach", c.CoachId)).GetAsync($"/messages/{c.ConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await h.LoginAsync("athlete", c.AthleteId)).GetAsync($"/messages/{c.ConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await h.LoginAsync("coach", otherCoach)).GetAsync($"/messages/{c.ConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await h.LoginAsync("athlete", other.AthleteId)).GetAsync($"/messages/{c.ConversationId}")).StatusCode);

        var anon = await app.Client().GetAsync($"/messages/{c.ConversationId}");
        Assert.Equal(HttpStatusCode.Redirect, anon.StatusCode);
        Assert.StartsWith("/demo", anon.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await (await h.LoginAsync("coach", c.CoachId)).GetAsync("/messages/999999")).StatusCode);
    }

    [Fact]
    public async Task Send_needs_antiforgery_and_a_participant()
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var before = Messages(c.ConversationId).Count;
        var coach = await h.LoginAsync("coach", c.CoachId);
        var intruder = await h.LoginAsync("coach", chat.FreeCoach(1));

        var noToken = await h.PostAsync(coach, $"/messages/{c.ConversationId}/send", new() { ["body"] = "Γεια" }, withToken: false);
        var foreign = await h.PostAsync(intruder, $"/messages/{c.ConversationId}/send", new() { ["body"] = "Γεια" }, tokenPage: "/messages");
        var anon = await h.PostAsync(app.Client(), $"/messages/{c.ConversationId}/send", new() { ["body"] = "Γεια" }, tokenPage: "/demo");

        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, anon.StatusCode);
        Assert.StartsWith("/demo", anon.Headers.Location!.OriginalString);
        Assert.Equal(before, Messages(c.ConversationId).Count);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData(null, "too-long")]
    public async Task Send_validates_the_body(string? body, string status)
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var before = Messages(c.ConversationId).Count;
        var coach = await h.LoginAsync("coach", c.CoachId);

        var response = await h.PostAsync(coach, $"/messages/{c.ConversationId}/send", new() { ["body"] = body ?? new string('α', 1001) }, tokenPage: "/messages");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"/messages/{c.ConversationId}?status={status}", response.Headers.Location!.OriginalString);
        Assert.Equal(before, Messages(c.ConversationId).Count);
        var html = await Html(coach, response.Headers.Location!.OriginalString);
        Assert.Matches($"<p[^>]*class=\"bp-msg-error\"[^>]*data-error=\"{status}\"", html);
    }

    [Fact]
    public async Task Send_stores_the_message_and_redirects_to_the_newest()
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var coach = await h.LoginAsync("coach", c.CoachId);
        var text = new string('β', 1000);

        var response = await h.PostAsync(coach, $"/messages/{c.ConversationId}/send", new() { ["body"] = "  Τα λέμε στο γήπεδο!  " }, tokenPage: "/messages");
        var max = await h.PostAsync(coach, $"/messages/{c.ConversationId}/send", new() { ["body"] = text }, tokenPage: "/messages");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/messages/{c.ConversationId}#bottom", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, max.StatusCode);
        var stored = Messages(c.ConversationId);
        Assert.Equal("Τα λέμε στο γήπεδο!", stored[^2].Body);
        Assert.Equal(MessageSender.Coach, stored[^2].SenderKind);
        Assert.Equal(text, stored[^1].Body);
        Assert.Null(stored[^1].ReadAt);

        var html = await Html(coach, $"/messages/{c.ConversationId}");
        var last = Bubbles(Thread(html))[^1];
        Assert.Contains("bp-msg-mine", last);
        Assert.Contains(text, last);
        Assert.Contains("id=\"bottom\"", html);
        Assert.Matches($"<form[^>]*method=\"post\"[^>]*action=\"/messages/{c.ConversationId}/send\"", html);
        Assert.Matches("<textarea[^>]*name=\"body\"[^>]*maxlength=\"1000\"", html);
    }

    // ---------- read receipts + unread ----------

    [Fact]
    public async Task Opening_a_thread_marks_the_other_side_read_and_shows_the_receipt()
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var coach = await h.LoginAsync("coach", c.CoachId);
        var athlete = await h.LoginAsync("athlete", c.AthleteId);

        // The athlete's last message is unread: the coach's nav shows 1, the athlete's own nav 0.
        Assert.Equal(1, NavUnread(await Html(coach, "/match")));
        Assert.Equal(0, NavUnread(await Html(athlete, "/me/inbox")));
        // The athlete's view: her message is not read yet -> "Εστάλη".
        Assert.Matches("<p[^>]*class=\"bp-msg-receipt\"[^>]*data-receipt=\"sent\"", Thread(await Html(athlete, $"/messages/{c.ConversationId}")));

        await Html(coach, $"/messages/{c.ConversationId}");

        var rows = Messages(c.ConversationId);
        Assert.NotNull(rows.Single(m => m.SenderKind == MessageSender.Athlete).ReadAt);
        Assert.Equal(0, NavUnread(await Html(coach, "/match")));
        var thread = Thread(await Html(athlete, $"/messages/{c.ConversationId}"));
        Assert.Matches("<p[^>]*class=\"bp-msg-receipt\"[^>]*data-receipt=\"read\"[^>]*>\\s*Διαβάστηκε", thread);

        // The coach sends: the athlete's nav shows 1 until she opens the thread.
        await h.PostAsync(coach, $"/messages/{c.ConversationId}/send", new() { ["body"] = "Τέλεια!" }, tokenPage: "/messages");
        Assert.Equal(1, NavUnread(await Html(athlete, "/me/inbox")));
        await Html(athlete, $"/messages/{c.ConversationId}");
        Assert.Equal(0, NavUnread(await Html(athlete, "/me/inbox")));
        Assert.All(Messages(c.ConversationId), m => Assert.NotNull(m.ReadAt));
    }

    [Fact]
    public async Task List_shows_conversations_newest_first_with_unread_and_the_messages_nav_link()
    {
        using var chat = new Chat(app);
        var coach = chat.FreeCoach(0);
        var now = DateTimeOffset.UtcNow;
        var older = chat.Interest(coach, InterestReply.Accepted, skip: 0);
        var newer = chat.Interest(coach, InterestReply.Accepted, skip: 0);
        var olderId = chat.Conversation(older.InterestId, (MessageSender.Athlete, "Παλιό μήνυμα", now.AddDays(-2), now.AddDays(-2).AddMinutes(5)));
        var newerId = chat.Conversation(newer.InterestId,
            (MessageSender.Coach, "Καλησπέρα", now.AddHours(-3), now.AddHours(-2)),
            (MessageSender.Athlete, "Νέο μήνυμα ένα", now.AddMinutes(-10), null),
            (MessageSender.Athlete, "Νέο μήνυμα δύο", now.AddMinutes(-5), null));
        var client = await h.LoginAsync("coach", coach);

        var html = await Html(client, "/messages");

        Assert.Equal(1, NavUnread(html));   // conversations with unread messages, not messages
        Assert.Matches("<a[^>]*href=\"/messages\"[^>]*>Μηνύματα \\(1\\)</a>", html);
        var items = Regex.Matches(html, "<a[^>]*class=\"bp-msg-item[^\"]*\"[^>]*data-conversation-id=\"(\\d+)\"[^>]*data-unread=\"(\\d+)\"")
            .Select(m => (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))).ToList();
        Assert.Equal([(newerId, 2), (olderId, 0)], items);
        Assert.Contains("Νέο μήνυμα δύο", html);
        Assert.Contains("<svg class=\"bp-jersey", html);
        // /messages previews the newest thread without marking it read.
        Assert.Contains($"data-conversation-id=\"{newerId}\"", Thread(html));
        Assert.All(Messages(newerId).Where(m => m.SenderKind == MessageSender.Athlete), m => Assert.Null(m.ReadAt));
        Assert.Contains("data-view=\"list\"", html);
        Assert.Contains("data-view=\"thread\"", await Html(client, $"/messages/{olderId}"));
    }

    [Fact]
    public async Task Thread_has_day_separators_header_and_connection_chip()
    {
        using var chat = new Chat(app);
        var coach = chat.FreeCoach(0);
        var i = chat.Interest(coach, InterestReply.Accepted);
        var now = DateTimeOffset.UtcNow;
        var id = chat.Conversation(i.InterestId,
            (MessageSender.Coach, "Πριν από μια εβδομάδα", now.AddDays(-8), now.AddDays(-8).AddHours(1)),
            (MessageSender.Athlete, "Χθεσινό", now.AddDays(-1), now.AddDays(-1).AddSeconds(30)),
            (MessageSender.Coach, "Σημερινό", now.AddSeconds(-5), null));
        var client = await h.LoginAsync("coach", coach);

        var thread = Thread(await Html(client, $"/messages/{id}"));

        var days = Regex.Matches(thread, "<li[^>]*class=\"bp-msg-day\"[^>]*>\\s*<span>([^<]+)</span>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(3, days.Count);
        Assert.Equal(["Χθες", "Σήμερα"], days[1..]);
        Assert.Matches("<a[^>]*href=\"/p/" + i.Slug + "\"", thread);   // the athlete's name links to the profile
        Assert.Matches("class=\"bp-chip-athlete bp-msg-since\"[^>]*>Σύνδεση από ", thread);
        Assert.Contains("href=\"/messages\"", thread);                  // back link (390 px)
    }

    [Fact]
    public async Task Empty_state_has_a_photo_and_the_role_next_step()
    {
        using var chat = new Chat(app);
        var coach = chat.FreeCoach(0);
        var lone = chat.Interest(coach, InterestReply.None);

        var coachHtml = await Html(await h.LoginAsync("coach", coach), "/messages");
        var athleteHtml = await Html(await h.LoginAsync("athlete", lone.AthleteId), "/messages");

        Assert.Contains("data-msg-empty", coachHtml);
        Assert.Matches(new Regex("data-msg-empty.*?<img[^>]*src=\"/media/stock/", RegexOptions.Singleline), coachHtml);
        Assert.Matches("<a[^>]*class=\"bp-btn-primary\"[^>]*href=\"/match\"", coachHtml);
        Assert.Contains("data-msg-empty", athleteHtml);
        Assert.Matches("<a[^>]*class=\"bp-btn-athlete\"[^>]*href=\"/me/inbox\"", athleteHtml);
    }

    // ---------- the connection is the permission ----------

    [Fact]
    public async Task No_conversation_without_an_accepted_interest()
    {
        using var chat = new Chat(app);
        var coach = chat.FreeCoach(0);
        var pending = chat.Interest(coach, InterestReply.None, skip: 0);
        var declined = chat.Interest(coach, InterestReply.Declined, skip: 0);
        var accepted = chat.Interest(coach, InterestReply.Accepted, skip: 0);
        var client = await h.LoginAsync("coach", coach);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/messages/with/{pending.InterestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/messages/with/{declined.InterestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await h.LoginAsync("coach", chat.FreeCoach(1))).GetAsync($"/messages/with/{accepted.InterestId}")).StatusCode);
        Assert.Equal(0, h.Db(db => db.Set<Conversation>().Count(c => c.CoachId == coach)));

        // An accepted interest without a conversation (e.g. accepted before messages existed) gets one on first open.
        var open = await client.GetAsync($"/messages/with/{accepted.InterestId}");
        Assert.Equal(HttpStatusCode.Redirect, open.StatusCode);
        var id = h.Db(db => db.Set<Conversation>().Single(c => c.InterestId == accepted.InterestId).Id);
        Assert.Equal($"/messages/{id}#bottom", open.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await (await h.LoginAsync("athlete", accepted.AthleteId)).GetAsync($"/messages/with/{accepted.InterestId}")).StatusCode);
        Assert.Equal(1, h.Db(db => db.Set<Conversation>().Count(c => c.InterestId == accepted.InterestId)));
    }

    [Fact]
    public async Task Accepting_creates_the_conversation_with_the_note_as_the_first_coach_message()
    {
        using var chat = new Chat(app);
        var coach = chat.FreeCoach(0);
        var withNote = chat.Interest(coach, InterestReply.None, note: "Θα θέλαμε να σας δούμε σε μια προπόνηση.", skip: 0);
        var athlete = await h.LoginAsync("athlete", withNote.AthleteId);

        var response = await h.PostAsync(athlete, $"/me/inbox/{withNote.InterestId}/reply", new() { ["reply"] = "accept" }, tokenPage: "/me/inbox");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var conversation = h.Db(db => db.Set<Conversation>().Include(c => c.Messages).AsNoTracking().Single(c => c.InterestId == withNote.InterestId));
        Assert.Equal((coach, withNote.AthleteId), (conversation.CoachId, conversation.AthleteId));
        var first = Assert.Single(conversation.Messages);
        Assert.Equal((MessageSender.Coach, "Θα θέλαμε να σας δούμε σε μια προπόνηση."), (first.SenderKind, first.Body));
        Assert.NotNull(first.ReadAt);   // she read the note before accepting

        var noNote = chat.Interest(coach, InterestReply.None, skip: 0);
        var other = await h.LoginAsync("athlete", noNote.AthleteId);
        await h.PostAsync(other, $"/me/inbox/{noNote.InterestId}/reply", new() { ["reply"] = "accept" }, tokenPage: "/me/inbox");
        Assert.Empty(h.Db(db => db.Set<Conversation>().Include(c => c.Messages).Single(c => c.InterestId == noNote.InterestId).Messages));

        var declined = chat.Interest(coach, InterestReply.None, note: "x", skip: 0);
        var third = await h.LoginAsync("athlete", declined.AthleteId);
        await h.PostAsync(third, $"/me/inbox/{declined.InterestId}/reply", new() { ["reply"] = "decline" }, tokenPage: "/me/inbox");
        Assert.False(h.Db(db => db.Set<Conversation>().Any(c => c.InterestId == declined.InterestId)));
    }

    // ---------- safety ----------

    [Fact]
    public async Task Athlete_block_closes_the_conversation_for_the_coach()
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var coach = await h.LoginAsync("coach", c.CoachId);
        var athlete = await h.LoginAsync("athlete", c.AthleteId);

        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(athlete, $"/messages/{c.ConversationId}/block", new(), withToken: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(coach, $"/messages/{c.ConversationId}/block", new(), tokenPage: "/messages")).StatusCode);
        var block = await h.PostAsync(athlete, $"/messages/{c.ConversationId}/block", new(), tokenPage: "/messages");
        Assert.Equal(HttpStatusCode.Redirect, block.StatusCode);
        Assert.NotNull(h.Db(db => db.Set<Conversation>().Single(x => x.Id == c.ConversationId).BlockedAt));

        var before = Messages(c.ConversationId).Count;
        var send = await h.PostAsync(coach, $"/messages/{c.ConversationId}/send", new() { ["body"] = "Γεια;" }, tokenPage: "/messages");
        Assert.Equal(HttpStatusCode.Redirect, send.StatusCode);
        Assert.Contains("status=closed", send.Headers.Location!.OriginalString);
        Assert.Equal(before, Messages(c.ConversationId).Count);

        var coachView = await Html(coach, $"/messages/{c.ConversationId}");
        Assert.Contains("data-closed", coachView);
        Assert.DoesNotContain($"action=\"/messages/{c.ConversationId}/send\"", coachView);
        Assert.DoesNotContain($"action=\"/messages/{c.ConversationId}/send\"", await Html(athlete, $"/messages/{c.ConversationId}"));
    }

    [Fact]
    public async Task Athlete_report_is_stored_once_with_its_reason()
    {
        using var chat = new Chat(app);
        var c = chat.Connected();
        var coach = await h.LoginAsync("coach", c.CoachId);
        var athlete = await h.LoginAsync("athlete", c.AthleteId);
        var page = await Html(athlete, $"/messages/{c.ConversationId}");
        Assert.Matches($"<form[^>]*method=\"post\"[^>]*action=\"/messages/{c.ConversationId}/report\"", page);
        Assert.DoesNotContain($"action=\"/messages/{c.ConversationId}/report\"", await Html(coach, $"/messages/{c.ConversationId}"));

        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(athlete, $"/messages/{c.ConversationId}/report", new() { ["reason"] = "harassment" }, withToken: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(coach, $"/messages/{c.ConversationId}/report", new() { ["reason"] = "harassment" }, tokenPage: "/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(athlete, $"/messages/{c.ConversationId}/report", new() { ["reason"] = "robot" }, tokenPage: "/messages")).StatusCode);
        var ok = await h.PostAsync(athlete, $"/messages/{c.ConversationId}/report", new() { ["reason"] = "harassment" }, tokenPage: "/messages");
        var again = await h.PostAsync(athlete, $"/messages/{c.ConversationId}/report", new() { ["reason"] = "spam" }, tokenPage: "/messages");

        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Contains("status=reported", ok.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        var report = Assert.Single(h.Db(db => db.Set<ConversationReport>().Where(r => r.ConversationId == c.ConversationId).ToList()));
        Assert.Equal("harassment", report.Reason);
    }

    // ---------- entry points ----------

    [Fact]
    public async Task Inbox_sent_and_profile_link_to_the_conversation_once_connected()
    {
        using var chat = new Chat(app);
        var coach = chat.FreeCoach(0);
        var accepted = chat.Interest(coach, InterestReply.Accepted, skip: 0);
        var pending = chat.Interest(coach, InterestReply.None, skip: 0);
        var link = $"/messages/with/{accepted.InterestId}";

        var inbox = await Html(await h.LoginAsync("athlete", accepted.AthleteId), "/me/inbox");
        var card = Regex.Match(inbox, $"<li[^>]*class=\"interest\"[^>]*data-interest-id=\"{accepted.InterestId}\".*?</li>", RegexOptions.Singleline).Value;
        Assert.Matches($"<a[^>]*class=\"bp-btn-athlete\"[^>]*href=\"{link}\"[^>]*>Στείλτε μήνυμα</a>", card);
        Assert.Matches("<a[^>]*class=\"bp-btn-ghost\"[^>]*href=\"mailto:contact@[a-z0-9-]+\\.invalid\"", card);

        var coachClient = await h.LoginAsync("coach", coach);
        var sent = await Html(coachClient, "/me/sent");
        var sentCard = Regex.Match(sent, $"<article[^>]*data-interest-id=\"{accepted.InterestId}\".*?</article>", RegexOptions.Singleline).Value;
        Assert.Matches($"<a[^>]*href=\"{link}\"[^>]*>Μήνυμα</a>", sentCard);
        var pendingCard = Regex.Match(sent, $"<article[^>]*data-interest-id=\"{pending.InterestId}\".*?</article>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("/messages/with/", pendingCard);

        Assert.Matches($"<a[^>]*href=\"{link}\"[^>]*>Μήνυμα</a>", await Html(coachClient, $"/p/{accepted.Slug}"));
        Assert.DoesNotContain("/messages/with/", await Html(coachClient, $"/p/{pending.Slug}"));
    }

    [Fact]
    public async Task Both_roles_have_the_messages_nav_link()
    {
        var coach = await Html(await h.LoginAsync("coach", h.CoachId(0)), "/match");
        var athlete = await Html(await h.LoginAsync("athlete", h.AthleteId(0)), "/me/inbox");
        foreach (var html in new[] { coach, athlete })
        {
            var nav = Regex.Match(html, "<nav[^>]*class=\"bp-nav\"[^>]*>(.*?)</nav>", RegexOptions.Singleline).Groups[1].Value;
            Assert.Matches("<a[^>]*href=\"/messages\"[^>]*>Μηνύματα", nav);
        }
        Assert.Matches("<a[^>]*href=\"/messages\"[^>]*aria-current=\"page\"", await Html(await h.LoginAsync("coach", h.CoachId(0)), "/messages"));
    }

    // ---------- seed ----------

    private static readonly DateTimeOffset Reset = new(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);

    private (DemoDataSet Data, MessagesSeedSet Seed) Build()
    {
        var data = DemoDataGenerator.Generate(42);
        InterestLifecycleSeeder.Apply(data.Interests, Reset, 42);
        return (data, MessagesSeeder.Build(data, Reset, Featured, 42));
    }

    private static readonly Regex Forbidden = new("(?i)demo|δείγμα|seeded|fictional|φανταστ|@|https?://|\\d{3}[ -]?\\d{3}[ -]?\\d{4}|\\b69\\d{8}\\b|\\b2\\d{9}\\b");

    [Fact]
    public void Seed_is_deterministic_and_keeps_its_invariants()
    {
        var (data, seed) = Build();
        var (_, again) = Build();
        var f = Featured;
        Assert.False(string.IsNullOrWhiteSpace(f.CoachFirstName), "Demo:Featured is not configured");

        Assert.InRange(seed.Conversations.Count, 23, 27);
        Assert.Equal(seed.Conversations.Select(c => (c.Coach!.LastName, c.Athlete!.Slug, c.Messages.Count, c.Messages[^1].SentAt, c.Messages[^1].Body)),
            again.Conversations.Select(c => (c.Coach!.LastName, c.Athlete!.Slug, c.Messages.Count, c.Messages[^1].SentAt, c.Messages[^1].Body)));

        var all = data.Interests.Concat(seed.NewInterests).ToList();
        Assert.Equal(all.Count, all.Select(i => (i.Coach, i.Athlete)).Distinct().Count());   // one interest per pair
        Assert.All(seed.NewInterests, i => Assert.Equal(i.Coach!.Club!.Gender, i.Athlete!.Gender));
        Assert.All(seed.NewInterests, i => Assert.NotEqual(i.Coach!.Club, i.Athlete!.CurrentClub));
        Assert.Equal(data.Interests.Count(i => i.Reply == InterestReply.Accepted) + seed.NewInterests.Count, seed.Conversations.Count);

        foreach (var c in seed.Conversations)
        {
            Assert.Equal(InterestReply.Accepted, c.Interest!.Reply);
            Assert.Same(c.Interest.Coach, c.Coach);
            Assert.Same(c.Interest.Athlete, c.Athlete);
            Assert.InRange(c.Messages.Count, 3, 14);
            Assert.True(c.Messages.Zip(c.Messages.Skip(1)).All(p => p.First.SentAt < p.Second.SentAt), "messages in time order");
            Assert.All(c.Messages, m => Assert.InRange(m.SentAt, Reset.AddDays(-23), Reset));
            Assert.All(c.Messages, m => Assert.InRange(m.Body.Length, 1, Message.MaxBody));
            Assert.All(c.Messages, m => Assert.False(Forbidden.IsMatch(m.Body), m.Body));
            Assert.All(c.Messages.Where(m => m.ReadAt is not null), m => Assert.InRange(m.ReadAt!.Value, m.SentAt, Reset));
            Assert.All(c.Messages, m => Assert.True(m.SentAt == c.Interest.CreatedAt || m.SentAt > c.Interest.RepliedAt, "only the note precedes the accept"));
        }
        Assert.Contains(seed.Conversations, c => c.Messages[^1].SentAt > Reset.AddMinutes(-15));
        Assert.Contains(seed.Conversations, c => c.Messages[^1].SentAt < Reset.AddDays(-7));
        Assert.Contains(seed.Conversations, c => c.Messages.Any(m => m.Body.Contains("Τα λέμε την Τρίτη στο γήπεδο!")));
        Assert.Contains(seed.Conversations, c => c.Messages.Any(m => m.Body.Contains("θα το συζητήσω με την οικογένειά μου", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(seed.Conversations, c => c.Athlete!.Gender == Gender.Male);
        Assert.Contains(seed.Conversations, c => GreekNames.IsFemaleFirstName(c.Coach!.FirstName));

        static bool UnreadFor(Conversation c, MessageSender reader) => c.Messages.Any(m => m.SenderKind != reader && m.ReadAt is null);
        Assert.Contains(seed.Conversations, c => UnreadFor(c, MessageSender.Coach));
        Assert.Contains(seed.Conversations, c => UnreadFor(c, MessageSender.Athlete));
        Assert.Contains(seed.Conversations, c => !UnreadFor(c, MessageSender.Coach) && !UnreadFor(c, MessageSender.Athlete));

        var featuredCoach = seed.Conversations.Where(c => c.Coach!.FirstName == f.CoachFirstName && c.Coach.LastName == f.CoachLastName).ToList();
        Assert.True(featuredCoach.Count >= 6, $"featured coach has {featuredCoach.Count} conversations");
        Assert.Equal(2, featuredCoach.Count(c => UnreadFor(c, MessageSender.Coach)));
        var featuredAthlete = seed.Conversations.Where(c => c.Athlete!.Slug == f.AthleteSlug).ToList();
        Assert.Equal(2, featuredAthlete.Count);
        Assert.DoesNotContain(featuredAthlete, c => c.Coach!.FirstName == f.CoachFirstName && c.Coach.LastName == f.CoachLastName);
        Assert.DoesNotContain(all, i => i.Athlete!.Slug == f.AthleteSlug && i.Coach!.FirstName == f.CoachFirstName && i.Coach.LastName == f.CoachLastName);
    }

    [Fact]
    public void Seed_addresses_men_and_women_correctly()
    {
        var (_, seed) = Build();
        foreach (var c in seed.Conversations)
        {
            var text = string.Join("\n", c.Messages.Select(m => m.Body));
            if (c.Athlete!.Gender == Gender.Male)
                Assert.DoesNotMatch("(?<!\\p{L})(έτοιμη|διαθέσιμη|ελεύθερη|σίγουρη|τις κοπέλες|τα κορίτσια)(?!\\p{L})", text);
            else
                Assert.DoesNotMatch("(?<!\\p{L})(έτοιμος|διαθέσιμος|ελεύθερος|σίγουρος|τα παιδιά της ομάδας)(?!\\p{L})", text);
            Assert.DoesNotContain("{", text);
        }
    }

    [Fact]
    public void Seeded_database_matches_the_build()
    {
        var f = Featured;
        var (convs, msgs, orphans, featuredCoach, featuredAthlete) = h.Db(db => (
            db.Set<Conversation>().Count(c => c.IsDemo),
            db.Set<Message>().Count(m => m.IsDemo),
            db.Set<Conversation>().Count(c => c.Interest!.Reply != InterestReply.Accepted || c.Interest.CoachId != c.CoachId || c.Interest.AthleteId != c.AthleteId),
            db.Set<Conversation>().Count(c => c.Coach!.FirstName == f.CoachFirstName && c.Coach.LastName == f.CoachLastName),
            db.Set<Conversation>().Count(c => c.Athlete!.Slug == f.AthleteSlug)));

        Assert.InRange(convs, 23, 27);
        Assert.InRange(msgs, convs * 3, convs * 14);
        Assert.Equal(0, orphans);
        Assert.True(featuredCoach >= 6);
        Assert.Equal(2, featuredAthlete);
    }

    // ---------- copy ----------

    private static readonly Regex Singular = new(
        @"(?<!\p{L})(Δες|Βρες|Διάλεξε|Ψάχνεις|Μπες|Στείλε|Γράψε|Έχεις|Πάτα|Επίλεξε|σου|εσένα)(?!\p{L})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void Messages_resource_is_formal_without_demo_words_and_has_english()
    {
        var manager = new ResourceManager("Ballpass.Web.Resources.MessagesResource", typeof(MessagesResource).Assembly);
        var el = manager.GetResourceSet(new CultureInfo("el"), createIfNotExists: true, tryParents: true)!;
        var en = manager.GetResourceSet(new CultureInfo("en"), createIfNotExists: true, tryParents: false)!;
        var offenders = new List<string>();
        var keys = new List<string>();
        foreach (System.Collections.DictionaryEntry e in el)
        {
            keys.Add((string)e.Key);
            if (e.Value is string text && Singular.Match(text) is { Success: true } m) offenders.Add($"{e.Key}: «{m.Value}»");
            if (e.Value is string t2 && Regex.IsMatch(t2, "(?i)demo|επίδειξ|δείγμα|φανταστικ|seeded")) offenders.Add($"{e.Key}: demo word");
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
        Assert.NotEmpty(keys);
        foreach (var k in keys) Assert.False(string.IsNullOrEmpty(en.GetString(k)), $"{k} has no English text");
    }
}
