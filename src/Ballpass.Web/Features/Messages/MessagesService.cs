using System.Security.Claims;
using Ballpass.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ballpass.Web.Features.Messages;

/// <summary>Config section Messages. TimeZone = the IANA zone for message times and day separators.</summary>
public class MessagesOptions
{
    public string TimeZone { get; set; } = "UTC";

    public TimeZoneInfo Zone => TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var tz) ? tz : TimeZoneInfo.Utc;
}

/// <summary>Who is looking: the role decides which side of a conversation is "mine".</summary>
public readonly record struct Viewer(MessageSender Role, int Id)
{
    public static Viewer? From(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true || !int.TryParse(user.FindFirstValue("entity_id"), out var id)) return null;
        if (user.IsInRole("Coach")) return new(MessageSender.Coach, id);
        if (user.IsInRole("Athlete")) return new(MessageSender.Athlete, id);
        return null;
    }

    public bool Owns(Conversation c) => Role == MessageSender.Coach ? c.CoachId == Id : c.AthleteId == Id;

    public bool Owns(Interest i) => Role == MessageSender.Coach ? i.CoachId == Id : i.AthleteId == Id;
}

/// <summary>Unread conversations for the nav link (scoped: both nav renderings of a request share one query) and the
/// conversation start used by the accept endpoint and /messages/with/{interestId}.</summary>
public class MessagesService(IServiceScopeFactory scopes)
{
    private readonly Dictionary<(Viewer, int?), Task<int>> unread = [];

    /// <param name="opening">The conversation the current page is opening (and marking read): not counted.</param>
    public Task<int> UnreadConversationsAsync(Viewer viewer, int? opening)
    {
        lock (unread)
        {
            if (!unread.TryGetValue((viewer, opening), out var task)) unread[(viewer, opening)] = task = Load(viewer, opening);
            return task;
        }
    }

    private async Task<int> Load(Viewer viewer, int? opening)
    {
        // Own scope: the layout renders while the page's own queries may still be running on the request's context.
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var mine = viewer.Role == MessageSender.Coach
            ? db.Set<Conversation>().Where(c => c.CoachId == viewer.Id)
            : db.Set<Conversation>().Where(c => c.AthleteId == viewer.Id);
        return await mine.CountAsync(c => c.Id != opening && c.Messages.Any(m => m.SenderKind != viewer.Role && m.ReadAt == null));
    }

    /// <summary>
    /// The conversation of an accepted interest, created on first use. A coach note becomes the first coach message, sent when the
    /// interest was sent and read when the athlete saw it. Safe to call twice (unique InterestId).
    /// </summary>
    public static async Task<Conversation> StartAsync(BallpassDbContext db, Interest interest, DateTimeOffset now)
    {
        var existing = await db.Set<Conversation>().FirstOrDefaultAsync(c => c.InterestId == interest.Id);
        if (existing is not null) return existing;

        var conversation = new Conversation
        {
            InterestId = interest.Id, CoachId = interest.CoachId, AthleteId = interest.AthleteId, CreatedAt = interest.RepliedAt ?? now, IsDemo = true,
        };
        if (!string.IsNullOrWhiteSpace(interest.Note))
            conversation.Messages.Add(new Message
            {
                SenderKind = MessageSender.Coach, Body = interest.Note, SentAt = interest.CreatedAt, ReadAt = interest.SeenAt ?? now, IsDemo = true,
            });
        db.Add(conversation);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request created it first.
            db.ChangeTracker.Clear();
            return await db.Set<Conversation>().FirstAsync(c => c.InterestId == interest.Id);
        }
        return conversation;
    }
}
