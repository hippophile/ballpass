using Ballpass.Web.Data;
using Ballpass.Web.Features.Messages;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Root namespace on purpose: Program.cs already imports Ballpass.Web, so it needs no extra using for AddMessages/MapMessages.
namespace Ballpass.Web;

public static class MessagesSetup
{
    /// <summary>Report reasons (stored as the key).</summary>
    public static readonly string[] ReportReasons = ["harassment", "inappropriate", "spam", "other"];

    public static IServiceCollection AddMessages(this IServiceCollection services)
    {
        services.AddOptions<MessagesOptions>().BindConfiguration("Messages");
        services.AddScoped<MessagesService>();
        return services;
    }

    /// <summary>
    /// Plain-form POST endpoints (same rules as /p/{slug}/interest: explicit antiforgery 400, participant checks 403, local redirects)
    /// and the GET that opens the conversation of an accepted interest. The connection is the permission: no accepted interest, no chat.
    /// </summary>
    public static IEndpointRouteBuilder MapMessages(this IEndpointRouteBuilder app)
    {
        app.MapPost("/messages/{id:int}/send", async (int id, HttpContext http, IAntiforgery antiforgery, BallpassDbContext db, TimeProvider time) =>
        {
            if (!await antiforgery.IsValidAsync(http)) return Results.BadRequest();
            if (http.User.Identity?.IsAuthenticated != true) return Results.Redirect("/demo");
            if (Viewer.From(http.User) is not { } viewer) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var conversation = await db.Set<Conversation>().FirstOrDefaultAsync(c => c.Id == id);
            if (conversation is null) return Results.NotFound();
            if (!viewer.Owns(conversation)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (conversation.BlockedAt is not null) return Results.Redirect($"/messages/{id}?status=closed");
            var form = await http.Request.ReadFormAsync();
            var body = form["body"].ToString().Trim();
            if (body.Length == 0) return Results.Redirect($"/messages/{id}?status=empty#composer");
            if (body.Length > Message.MaxBody) return Results.Redirect($"/messages/{id}?status=too-long#composer");

            var now = time.GetUtcNow();
            // Replying means the other side's messages were read.
            await db.Set<Message>().Where(m => m.ConversationId == id && m.SenderKind != viewer.Role && m.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, now));
            db.Add(new Message { ConversationId = id, SenderKind = viewer.Role, Body = body, SentAt = now, IsDemo = true });
            await db.SaveChangesAsync();
            return Results.Redirect($"/messages/{id}#bottom");
        }).DisableAntiforgery();

        app.MapPost("/messages/{id:int}/block", async (int id, HttpContext http, IAntiforgery antiforgery, BallpassDbContext db, TimeProvider time) =>
        {
            var (conversation, error) = await AthleteConversation(id, http, antiforgery, db);
            if (error is not null) return error;
            conversation.BlockedAt ??= time.GetUtcNow();
            await db.SaveChangesAsync();
            return Results.Redirect($"/messages/{id}?status=blocked");
        }).DisableAntiforgery();

        app.MapPost("/messages/{id:int}/report", async (int id, HttpContext http, IAntiforgery antiforgery, BallpassDbContext db, TimeProvider time) =>
        {
            var (conversation, error) = await AthleteConversation(id, http, antiforgery, db);
            if (error is not null) return error;
            var form = await http.Request.ReadFormAsync();
            var reason = form["reason"].ToString();
            if (!ReportReasons.Contains(reason)) return Results.BadRequest();

            var now = time.GetUtcNow();
            if (form["block"] == "true") conversation.BlockedAt ??= now;
            if (!await db.Set<ConversationReport>().AnyAsync(r => r.ConversationId == id))
                db.Add(new ConversationReport { ConversationId = id, Reason = reason, CreatedAt = now, IsDemo = true });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // A concurrent report already stored it: one report per conversation.
            }
            return Results.Redirect($"/messages/{id}?status=reported");
        }).DisableAntiforgery();

        app.MapGet("/messages/with/{interestId:int}", async (int interestId, HttpContext http, BallpassDbContext db, TimeProvider time) =>
        {
            if (http.User.Identity?.IsAuthenticated != true) return Results.Redirect($"/demo?ReturnUrl={Uri.EscapeDataString(http.Request.Path)}");
            if (Viewer.From(http.User) is not { } viewer) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var interest = await db.Interests.FirstOrDefaultAsync(i => i.Id == interestId);
            if (interest is null) return Results.NotFound();
            if (!viewer.Owns(interest)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (interest.Reply != InterestReply.Accepted) return Results.NotFound();
            var conversation = await MessagesService.StartAsync(db, interest, time.GetUtcNow());
            return Results.Redirect($"/messages/{conversation.Id}#bottom");
        });
        return app;
    }

    /// <summary>Block and report: antiforgery (400), signed in (else /demo), the conversation's athlete only (403).</summary>
    private static async Task<(Conversation Conversation, IResult? Error)> AthleteConversation(int id, HttpContext http, IAntiforgery antiforgery, BallpassDbContext db)
    {
        if (!await antiforgery.IsValidAsync(http)) return (null!, Results.BadRequest());
        if (http.User.Identity?.IsAuthenticated != true) return (null!, Results.Redirect("/demo"));
        if (Viewer.From(http.User) is not { Role: MessageSender.Athlete } viewer) return (null!, Results.StatusCode(StatusCodes.Status403Forbidden));
        var conversation = await db.Set<Conversation>().FirstOrDefaultAsync(c => c.Id == id);
        if (conversation is null) return (null!, Results.NotFound());
        if (!viewer.Owns(conversation)) return (null!, Results.StatusCode(StatusCodes.Status403Forbidden));
        return (conversation, null);
    }
}
