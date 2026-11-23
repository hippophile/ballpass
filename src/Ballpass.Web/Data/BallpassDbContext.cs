using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Data;

public class BallpassDbContext(DbContextOptions<BallpassDbContext> options) : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Athlete> Athletes => Set<Athlete>();
    public DbSet<AthleteSeason> AthleteSeasons => Set<AthleteSeason>();
    public DbSet<StatLine> StatLines => Set<StatLine>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<ShortlistEntry> ShortlistEntries => Set<ShortlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Club>(e =>
        {
            e.Property(x => x.Gender).HasConversion<string>();
            e.Property(x => x.League).HasConversion<string>();
        });

        modelBuilder.Entity<Coach>(e =>
        {
            e.Property(x => x.Role).HasConversion<string>();
            e.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId);
        });

        modelBuilder.Entity<Athlete>(e =>
        {
            e.Property(x => x.Gender).HasConversion<string>();
            e.Property(x => x.Position).HasConversion<string>();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.CurrentClub).WithMany().HasForeignKey(x => x.CurrentClubId).IsRequired(false);
            e.HasMany(x => x.Seasons).WithOne().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Stats).WithOne().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AthleteSeason>(e =>
        {
            e.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId);
        });

        modelBuilder.Entity<StatLine>(e =>
        {
            e.Property(x => x.League).HasConversion<string>();
            e.Property(x => x.Source).HasConversion<string>();
            e.Property(x => x.Minutes).HasPrecision(5, 1);
            e.Property(x => x.Points).HasPrecision(5, 1);
            e.Property(x => x.Rebounds).HasPrecision(5, 1);
            e.Property(x => x.Assists).HasPrecision(5, 1);
            e.Property(x => x.Steals).HasPrecision(5, 1);
            e.Property(x => x.Blocks).HasPrecision(5, 1);
            e.Property(x => x.FgPct).HasPrecision(4, 1);
            e.Property(x => x.ThreePct).HasPrecision(4, 1);
            e.Property(x => x.FtPct).HasPrecision(4, 1);
        });

        modelBuilder.Entity<Interest>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(280);
            e.Property(x => x.NoteEn).HasMaxLength(280);
            e.HasIndex(x => new { x.CoachId, x.AthleteId }).IsUnique();
            e.HasOne(x => x.Coach).WithMany().HasForeignKey(x => x.CoachId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShortlistEntry>(e =>
        {
            e.HasIndex(x => new { x.CoachId, x.AthleteId }).IsUnique();
            e.HasOne(x => x.Coach).WithMany().HasForeignKey(x => x.CoachId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BallpassDbContext).Assembly);
    }
}
