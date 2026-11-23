using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ballpass.Web.Data;

public enum MessageSender { Coach, Athlete }

/// <summary>
/// One conversation per accepted interest: the connection is the permission (founder decision 2026-11-23). Only the interest's coach
/// and athlete take part. A blocked conversation is closed: nobody can send.
/// </summary>
public class Conversation
{
    public int Id { get; set; }
    public int InterestId { get; set; }
    public Interest? Interest { get; set; }
    public int CoachId { get; set; }
    public Coach? Coach { get; set; }
    public int AthleteId { get; set; }
    public Athlete? Athlete { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Set by the athlete; the conversation is closed from then on.</summary>
    public DateTimeOffset? BlockedAt { get; set; }
    public bool IsDemo { get; set; }
    public List<Message> Messages { get; set; } = [];
}

public class Message
{
    public const int MaxBody = 1000;

    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public MessageSender SenderKind { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset SentAt { get; set; }
    /// <summary>Set when the other participant opens the thread; drives "Διαβάστηκε" and the unread counts.</summary>
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsDemo { get; set; }
}

/// <summary>The athlete's report of a conversation (one per conversation). Stored for moderation; there is no review UI in the POC.</summary>
public class ConversationReport
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> e)
    {
        e.HasIndex(x => x.InterestId).IsUnique();
        e.HasIndex(x => x.CoachId);
        e.HasIndex(x => x.AthleteId);
        e.HasOne(x => x.Interest).WithMany().HasForeignKey(x => x.InterestId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Coach).WithMany().HasForeignKey(x => x.CoachId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Messages).WithOne(x => x.Conversation).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> e)
    {
        e.Property(x => x.SenderKind).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Body).HasMaxLength(Message.MaxBody);
        e.HasIndex(x => new { x.ConversationId, x.SentAt });
    }
}

public class ConversationReportConfiguration : IEntityTypeConfiguration<ConversationReport>
{
    public void Configure(EntityTypeBuilder<ConversationReport> e)
    {
        e.Property(x => x.Reason).HasMaxLength(24);
        e.HasIndex(x => x.ConversationId).IsUnique();
        e.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
