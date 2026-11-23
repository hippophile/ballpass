using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ballpass.Web.Data;

public enum PostType { LookingForTeam, TryoutCall, StatUpdate, Highlight, Badge, FreeText }

public enum AuthorKind { Athlete, Coach, Club }

/// <summary>One feed post. Flat columns: only the fields of its <see cref="Type"/> are filled. Actors are keyed "athlete:{id}" / "coach:{id}" / "club:{id}".</summary>
public class Post
{
    public int Id { get; set; }
    public AuthorKind AuthorKind { get; set; }
    public int AuthorId { get; set; }
    public required string AuthorKey { get; set; }
    /// <summary>Set when a club "posts" through its head coach (seed only).</summary>
    public int? PostedByCoachId { get; set; }
    public PostType Type { get; set; }
    /// <summary>System posts (stat update, badge) are generated, not typed by the author.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Free text, or the short note of a looking-for-team post.</summary>
    public string? Text { get; set; }
    /// <summary>English forms of the seeded texts (null for posts typed at runtime); shown in English when present.</summary>
    public string? TextEn { get; set; }
    public string? TitleEn { get; set; }
    public string? PlaceEn { get; set; }
    public string? PositionsEn { get; set; }

    // Looking-for-team
    public Position? Position { get; set; }
    public string? Region { get; set; }
    public DateOnly? AvailableFrom { get; set; }

    // Tryout call
    public int? ClubId { get; set; }
    public DateOnly? EventDate { get; set; }
    public string? Place { get; set; }
    public string? Positions { get; set; }
    public DateOnly? Deadline { get; set; }

    // Stat update
    public string? Season { get; set; }
    public int? Games { get; set; }
    public decimal? Points { get; set; }
    public decimal? Rebounds { get; set; }
    public decimal? Assists { get; set; }
    public StatSource? Source { get; set; }

    // Highlight
    public string? Title { get; set; }
    public string? Tag { get; set; }

    // Badge
    public string? BadgeKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>CreatedAt plus the ranking boost, fixed at write time: the keyset cursor orders by (SortAt, Id).</summary>
    public DateTimeOffset SortAt { get; set; }
    public bool IsDemo { get; set; }
    /// <summary>Hidden for everyone (3 reports, pending review).</summary>
    public bool IsHidden { get; set; }
}

public class Kudos
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public required string ActorKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}

public class Follow
{
    public int Id { get; set; }
    public required string FollowerKey { get; set; }
    public required string TargetKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}

public class Block
{
    public int Id { get; set; }
    public required string BlockerKey { get; set; }
    public required string BlockedKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}

public class Report
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public required string ReporterKey { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}

/// <summary>A post one actor hid for themselves (also created by reporting).</summary>
public class PostHide
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public required string ActorKey { get; set; }
}

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> e)
    {
        e.Property(x => x.AuthorKind).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
        e.Property(x => x.Position).HasConversion<string>().HasMaxLength(4);
        e.Property(x => x.Source).HasConversion<string>().HasMaxLength(24);
        e.Property(x => x.AuthorKey).HasMaxLength(32);
        e.Property(x => x.Text).HasMaxLength(280);
        e.Property(x => x.TextEn).HasMaxLength(280);
        e.Property(x => x.TitleEn).HasMaxLength(80);
        e.Property(x => x.PlaceEn).HasMaxLength(120);
        e.Property(x => x.PositionsEn).HasMaxLength(40);
        e.Property(x => x.Region).HasMaxLength(80);
        e.Property(x => x.Place).HasMaxLength(120);
        e.Property(x => x.Positions).HasMaxLength(40);
        e.Property(x => x.Season).HasMaxLength(16);
        e.Property(x => x.Title).HasMaxLength(80);
        e.Property(x => x.Tag).HasMaxLength(40);
        e.Property(x => x.BadgeKey).HasMaxLength(40);
        e.Property(x => x.Points).HasPrecision(5, 1);
        e.Property(x => x.Rebounds).HasPrecision(5, 1);
        e.Property(x => x.Assists).HasPrecision(5, 1);
        e.HasIndex(x => new { x.CreatedAt, x.Id }).IsDescending(true, true);
        e.HasIndex(x => new { x.SortAt, x.Id }).IsDescending(true, true);
        e.HasIndex(x => new { x.AuthorKey, x.CreatedAt }).IsDescending(false, true);
    }
}

public class KudosConfiguration : IEntityTypeConfiguration<Kudos>
{
    public void Configure(EntityTypeBuilder<Kudos> e)
    {
        e.Property(x => x.ActorKey).HasMaxLength(32);
        e.HasIndex(x => new { x.PostId, x.ActorKey }).IsUnique();
        e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> e)
    {
        e.Property(x => x.FollowerKey).HasMaxLength(32);
        e.Property(x => x.TargetKey).HasMaxLength(32);
        e.HasIndex(x => new { x.FollowerKey, x.TargetKey }).IsUnique();
        e.HasIndex(x => x.TargetKey);
    }
}

public class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> e)
    {
        e.Property(x => x.BlockerKey).HasMaxLength(32);
        e.Property(x => x.BlockedKey).HasMaxLength(32);
        e.HasIndex(x => new { x.BlockerKey, x.BlockedKey }).IsUnique();
        e.HasIndex(x => x.BlockedKey);
    }
}

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> e)
    {
        e.Property(x => x.ReporterKey).HasMaxLength(32);
        e.Property(x => x.Reason).HasMaxLength(24);
        e.HasIndex(x => new { x.PostId, x.ReporterKey }).IsUnique();
        e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PostHideConfiguration : IEntityTypeConfiguration<PostHide>
{
    public void Configure(EntityTypeBuilder<PostHide> e)
    {
        e.Property(x => x.ActorKey).HasMaxLength(32);
        e.HasIndex(x => new { x.PostId, x.ActorKey }).IsUnique();
        e.HasIndex(x => x.ActorKey);
        e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
