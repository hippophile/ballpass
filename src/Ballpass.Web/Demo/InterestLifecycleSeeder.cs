using Ballpass.Web.Data;
using Bogus;

namespace Ballpass.Web.Demo;

/// <summary>
/// Gives about 30% of the seeded interests a SeenAt and about 15% a reply (a subset of the seen ones, mostly accepted).
/// Deterministic: one seeded Randomizer and an explicit reset time, never the clock. Timestamps fall strictly after
/// CreatedAt (and after SeenAt for replies) and never after the reset time.
/// </summary>
public static class InterestLifecycleSeeder
{
    public const double SeenShare = 0.30;
    public const double RepliedShare = 0.15;

    public static void Apply(IReadOnlyList<Interest> interests, DateTimeOffset resetTime, int seed)
    {
        var r = new Randomizer(seed + 7);
        var order = r.Shuffle(Enumerable.Range(0, interests.Count)).ToList();
        var seen = (int)Math.Round(interests.Count * SeenShare, MidpointRounding.AwayFromZero);
        var replied = (int)Math.Round(interests.Count * RepliedShare, MidpointRounding.AwayFromZero);

        for (var n = 0; n < seen; n++)
        {
            var interest = interests[order[n]];
            interest.SeenAt = After(interest.CreatedAt, resetTime, 0.05 + 0.45 * r.Double());
            if (n >= replied) continue;
            interest.Reply = n % 3 == 2 ? InterestReply.Declined : InterestReply.Accepted;
            interest.RepliedAt = After(interest.SeenAt.Value, resetTime, 0.10 + 0.80 * r.Double());
        }
    }

    // A fraction of the way from `from` to `to`; never earlier than `from` (and equal only when there is no room).
    private static DateTimeOffset After(DateTimeOffset from, DateTimeOffset to, double fraction)
    {
        var gap = to - from;
        if (gap <= TimeSpan.Zero) return from;
        return from + TimeSpan.FromTicks(Math.Max(1, (long)(gap.Ticks * fraction)));
    }
}
