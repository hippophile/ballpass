using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ballpass.Web.Data;

/// <summary>A flat comment on a post (no replies to replies). Actors are keyed "athlete:{id}" / "coach:{id}", like kudos.</summary>
public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public required string AuthorKey { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Hidden for everyone: by the post author, or by enough distinct reports.</summary>
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }
    public bool IsDemo { get; set; }
}

/// <summary>A report of one comment (same reason list as post reports). The reporter stops seeing the comment at once.</summary>
public class CommentReport
{
    public int Id { get; set; }
    public int CommentId { get; set; }
    public Comment? Comment { get; set; }
    public required string ReporterKey { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}

/// <summary>A hashtag of a post. <see cref="Tag"/> is the folded matching key (lower case, no accents); <see cref="Label"/> the lower-cased text as typed.</summary>
public class PostTag
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public required string Tag { get; set; }
    public required string Label { get; set; }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> e)
    {
        e.Property(x => x.AuthorKey).HasMaxLength(32);
        e.Property(x => x.Text).HasMaxLength(280);
        e.Property(x => x.HiddenReason).HasMaxLength(16);
        e.HasIndex(x => new { x.PostId, x.CreatedAt });
        e.HasIndex(x => new { x.AuthorKey, x.CreatedAt });
        e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentReportConfiguration : IEntityTypeConfiguration<CommentReport>
{
    public void Configure(EntityTypeBuilder<CommentReport> e)
    {
        e.Property(x => x.ReporterKey).HasMaxLength(32);
        e.Property(x => x.Reason).HasMaxLength(24);
        e.HasIndex(x => new { x.CommentId, x.ReporterKey }).IsUnique();
        e.HasIndex(x => x.ReporterKey);
        e.HasOne(x => x.Comment).WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PostTagConfiguration : IEntityTypeConfiguration<PostTag>
{
    public void Configure(EntityTypeBuilder<PostTag> e)
    {
        e.Property(x => x.Tag).HasMaxLength(40);
        e.Property(x => x.Label).HasMaxLength(40);
        e.HasIndex(x => new { x.PostId, x.Tag }).IsUnique();
        e.HasIndex(x => x.Tag);
        e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
