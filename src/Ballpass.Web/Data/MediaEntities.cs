using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ballpass.Web.Data;

public enum MediaKind { Photo, Video }

/// <summary>One photo or short video. Belongs to a post (0..4 per post) or to an athlete's highlight reel (1..3), never both.
/// <see cref="File"/> is relative to /media/ (self-hosted, wwwroot/media). Stock photos never show the named athlete: <see cref="IsGraphic"/> only picks the label.</summary>
public class MediaItem
{
    public int Id { get; set; }
    public int? PostId { get; set; }
    public Post? Post { get; set; }
    public int? AthleteId { get; set; }
    public Athlete? Athlete { get; set; }
    public int Position { get; set; }
    public MediaKind Kind { get; set; }
    public required string File { get; set; }
    /// <summary>WebP poster frame, videos only.</summary>
    public string? Poster { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public required string AltEl { get; set; }
    public required string AltEn { get; set; }
    /// <summary>True = generated motion graphic ("Γραφικό · demo"), false = stock photo ("Εικόνα αρχείου · demo").</summary>
    public bool IsGraphic { get; set; }
    public bool IsDemo { get; set; }
}

public class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> e)
    {
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(8);
        e.Property(x => x.File).HasMaxLength(120);
        e.Property(x => x.Poster).HasMaxLength(120);
        e.Property(x => x.AltEl).HasMaxLength(200);
        e.Property(x => x.AltEn).HasMaxLength(200);
        e.HasIndex(x => new { x.PostId, x.Position });
        e.HasIndex(x => new { x.AthleteId, x.Position });
        e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Cascade);
    }
}
