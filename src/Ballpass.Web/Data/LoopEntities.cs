using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ballpass.Web.Data;

public enum InterestReply { None, Accepted, Declined }

/// <summary>Loop package: lifecycle columns on Interest (registered through ApplyConfigurationsFromAssembly).</summary>
public class InterestLoopConfiguration : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> e)
    {
        e.Property(x => x.Reply).HasConversion<string>().HasMaxLength(16).HasDefaultValue(InterestReply.None);
    }
}
