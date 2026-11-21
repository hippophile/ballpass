using Ballpass.Web.Data;
using Bogus;

namespace Ballpass.Web.Demo;

public record DemoDataSet(IReadOnlyList<Club> Clubs, IReadOnlyList<Coach> Coaches, IReadOnlyList<Athlete> Athletes)
{
    public IReadOnlyList<Interest> Interests { get; init; } = [];
    public IReadOnlyList<ShortlistEntry> Shortlist { get; init; } = [];
}

// Pure generator: every random decision comes from one seeded Bogus Randomizer (no clock, no Guid, no unseeded Random),
// so the same seed always gives the same data set. All people and clubs are fictional.
public static class DemoDataGenerator
{
    private const int WomenCount = 200;
    private const int MenCount = 100;
    private const int HeadCoachCount = 20;
    private const int AssistantCoachCount = 10;

    private const int InterestCount = 40;

    private static readonly string[] InterestNotes =
    [
        "Θα θέλαμε να σας δούμε σε μια προπόνηση.",
        "Το προφίλ σας μας ενδιαφέρει. Επικοινωνήστε μαζί μας όποτε μπορείτε.",
        "Ψάχνουμε παίκτες με τα δικά σας χαρακτηριστικά για την επόμενη περίοδο.",
        "Θα χαρούμε να γνωριστούμε και να μιλήσουμε για τα σχέδιά σας.",
        "Παρακολουθούμε τις εμφανίσεις σας και θα θέλαμε μια συνάντηση.",
        "Η ομάδα μας διαθέτει θέση στο ρόστερ. Ελάτε να συζητήσουμε.",
        "Είστε ευπρόσδεκτη/ος σε μια δοκιμαστική προπόνηση.",
        "Μας άρεσε το προφίλ σας. Πείτε μας πότε είστε διαθέσιμη/ος.",
    ];

    private static readonly string[] Seasons = ["2023-24", "2024-25", "2025-26"];

    // Invented nicknames: deliberately not real club names.
    private static readonly string[] ClubNicknames =
    [
        "Αστραπές", "Φοίνικες", "Γλάροι", "Μέλισσες", "Κομήτες", "Τιτάνες", "Δελφίνια", "Σείριοι",
        "Αετοί", "Πυρσοί", "Κύματα", "Νεφέλες", "Βορέηδες", "Ηχώ", "Ορμητικοί", "Αλκυόνες",
        "Θύελλα", "Ζέφυροι", "Μαχητές", "Κορυφαίοι",
    ];

    private static readonly string[] Bios =
    [
        "{0} με έμφαση στην άμυνα και την ομαδική προσπάθεια. Ψάχνει συνεχή βελτίωση και ρόλο στην ομάδα.",
        "{0} από την πόλη {1}. Δουλεύει καθημερινά στην τεχνική και τη φυσική κατάσταση.",
        "{0} με καλή ανάγνωση του παιχνιδιού. Αγαπά το γρήγορο παιχνίδι και τη συνεργασία.",
        "{0} που παίζει με ενέργεια και πάθος. Στόχος η άνοδος κατηγορίας.",
        "{0} με εμπειρία σε πολλές αγωνιστικές περιόδους. Έτοιμη να προσφέρει από τον πάγκο ή στη βασική πεντάδα.",
    ];

    private static readonly Dictionary<Position, string> PositionBio = new()
    {
        [Position.PG] = "Πλέι μέικερ",
        [Position.SG] = "Σούτινγκ γκαρντ",
        [Position.SF] = "Σμολ φόργουορντ",
        [Position.PF] = "Πάουερ φόργουορντ",
        [Position.C] = "Σέντερ",
    };

    private static readonly Position[] PositionOrder = [Position.PG, Position.SG, Position.SF, Position.PF, Position.C];

    // Weights per position (PG, SG, SF, PF, C).
    private static readonly int[] PositionWeights = [20, 22, 22, 20, 16];

    private static readonly Dictionary<Position, (int Min, int Max)> WomenHeights = new()
    {
        [Position.PG] = (160, 178),
        [Position.SG] = (166, 184),
        [Position.SF] = (172, 190),
        [Position.PF] = (178, 195),
        [Position.C] = (185, 200),
    };

    private static readonly Dictionary<Position, (int Min, int Max)> MenHeights = new()
    {
        [Position.PG] = (175, 190),
        [Position.SG] = (180, 196),
        [Position.SF] = (188, 204),
        [Position.PF] = (195, 211),
        [Position.C] = (200, 220),
    };

    // Per-game centre values per position: points, rebounds, assists, steals, blocks, 3P% centre.
    private static readonly Dictionary<Position, (double Pts, double Reb, double Ast, double Stl, double Blk, double Three)> Profile = new()
    {
        [Position.PG] = (9.0, 2.8, 4.4, 1.6, 0.2, 31),
        [Position.SG] = (10.0, 3.2, 2.6, 1.3, 0.3, 32),
        [Position.SF] = (9.5, 4.6, 1.9, 1.1, 0.5, 29),
        [Position.PF] = (9.0, 6.4, 1.4, 0.9, 0.9, 25),
        [Position.C] = (8.5, 7.6, 1.0, 0.7, 1.4, 18),
    };

    public static DemoDataSet Generate(int seed = 42)
    {
        var r = new Randomizer(seed);

        var clubs = GenerateClubs(r);
        var coaches = GenerateCoaches(r, clubs);

        var athletes = new List<Athlete>(WomenCount + MenCount);
        var slugs = new HashSet<string>();
        for (var i = 0; i < WomenCount; i++) athletes.Add(GenerateAthlete(r, Gender.Female, clubs, slugs));
        for (var i = 0; i < MenCount; i++) athletes.Add(GenerateAthlete(r, Gender.Male, clubs, slugs));

        // Generated after the athletes so the athlete data stays identical to earlier seeds.
        var (interests, shortlist) = GenerateInterestAndShortlist(r, coaches, athletes);

        return new DemoDataSet(clubs, coaches, athletes) { Interests = interests, Shortlist = shortlist };
    }

    private static (List<Interest> Interests, List<ShortlistEntry> Shortlist) GenerateInterestAndShortlist(
        Randomizer r, List<Coach> coaches, List<Athlete> athletes)
    {
        var heads = coaches.Where(c => c.Role == CoachRole.Head).ToList();
        var baseTime = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

        var shortlist = new List<ShortlistEntry>();
        foreach (var coach in heads)
        {
            var pool = athletes.Where(a => a.Gender == coach.Club!.Gender).ToList();
            foreach (var athlete in r.Shuffle(pool).Take(r.Int(3, 5)))
                shortlist.Add(new ShortlistEntry
                {
                    Coach = coach,
                    Athlete = athlete,
                    CreatedAt = baseTime.AddMinutes(r.Int(0, 20 * 24 * 60)),
                    IsDemo = true,
                });
        }

        var interests = new List<Interest>();
        var pairs = new HashSet<(Coach, Athlete)>();
        while (interests.Count < InterestCount)
        {
            var coach = Pick(r, heads);
            var sameGender = athletes.Where(a => a.Gender == coach.Club!.Gender).ToList();
            var outside = sameGender.Where(a => a.CurrentClub != coach.Club).ToList();
            // Only athletes from other clubs: a coach "discovering" their own player looks odd in a demo (audit m10).
            // The draw is kept so the random sequence (and the rest of the data) stays the same.
            _ = r.Double();
            var athlete = Pick(r, outside);
            if (!pairs.Add((coach, athlete))) continue;
            var noteRoll = r.Int(0, InterestNotes.Length + 1);
            interests.Add(new Interest
            {
                Coach = coach,
                Athlete = athlete,
                Note = noteRoll < InterestNotes.Length ? InterestNotes[noteRoll] : null,
                CreatedAt = baseTime.AddMinutes(r.Int(0, 20 * 24 * 60)),
                IsDemo = true,
            });
        }
        return (interests, shortlist);
    }

    private static List<Club> GenerateClubs(Randomizer r)
    {
        var nicknames = r.Shuffle(ClubNicknames).ToList();
        // 12 women: 3 per league; 8 men: 2 per league.
        var leagues = new[] { League.A1, League.A2, League.B, League.Regional };
        var clubs = new List<Club>();
        var n = 0;
        foreach (var (gender, perLeague) in new[] { (Gender.Female, 3), (Gender.Male, 2) })
        {
            foreach (var league in leagues)
            {
                for (var k = 0; k < perLeague; k++)
                {
                    var city = GreekNames.Cities[(n * 5 + r.Int(0, 2)) % GreekNames.Cities.Count];
                    clubs.Add(new Club
                    {
                        Name = $"{nicknames[n]} {city.Genitive} (demo)",
                        City = city.Name,
                        Region = city.Region,
                        Gender = gender,
                        League = league,
                        IsDemo = true,
                    });
                    n++;
                }
            }
        }
        return clubs;
    }

    private static List<Coach> GenerateCoaches(Randomizer r, List<Club> clubs)
    {
        var coaches = new List<Coach>();
        foreach (var club in clubs.Take(HeadCoachCount)) coaches.Add(NewCoach(r, club, CoachRole.Head));
        foreach (var club in r.Shuffle(clubs).Take(AssistantCoachCount)) coaches.Add(NewCoach(r, club, CoachRole.Assistant));
        return coaches;
    }

    private static Coach NewCoach(Randomizer r, Club club, CoachRole role)
    {
        var female = r.Bool();
        var first = Pick(r, female ? GreekNames.FemaleFirstNames : GreekNames.MaleFirstNames);
        var last = Pick(r, GreekNames.Surnames);
        return new Coach
        {
            FirstName = first.Greek,
            LastName = female ? last.Female : last.Male,
            FirstNameLatin = first.Latin,
            LastNameLatin = female ? last.FemaleLatin : last.MaleLatin,
            Club = club,
            Role = role,
            Region = club.Region,
            IsDemo = true,
        };
    }

    private static Athlete GenerateAthlete(Randomizer r, Gender gender, List<Club> clubs, HashSet<string> slugs)
    {
        var female = gender == Gender.Female;
        // Unique full names: re-draw until the name's base slug is unused (no two "Άννα Αλεξίου").
        FirstName first;
        SurnamePair last;
        do
        {
            first = Pick(r, female ? GreekNames.FemaleFirstNames : GreekNames.MaleFirstNames);
            last = Pick(r, GreekNames.Surnames);
        } while (slugs.Contains($"{first.Latin}-{(female ? last.FemaleLatin : last.MaleLatin)}".ToLowerInvariant()));
        var position = PositionOrder[WeightedIndex(r, PositionWeights)];

        var (hMin, hMax) = (female ? WomenHeights : MenHeights)[position];
        // Average of two draws clusters heights near the middle of the range.
        var height = (r.Int(hMin, hMax) + r.Int(hMin, hMax)) / 2;

        var sameGender = clubs.Where(c => c.Gender == gender).ToList();
        var hasClub = r.Double() < 0.80;
        var currentClub = hasClub ? Pick(r, sameGender) : null;

        GreekCity city;
        if (currentClub is not null && r.Double() < 0.7)
            city = GreekNames.Cities.First(c => c.Name == currentClub.City);
        else
            city = Pick(r, GreekNames.Cities);

        var slug = UniqueSlug($"{first.Latin}-{(female ? last.FemaleLatin : last.MaleLatin)}".ToLowerInvariant(), slugs);

        var athlete = new Athlete
        {
            Slug = slug,
            FirstName = first.Greek,
            LastName = female ? last.Female : last.Male,
            FirstNameLatin = first.Latin,
            LastNameLatin = female ? last.FemaleLatin : last.MaleLatin,
            BirthYear = r.Int(1992, 2007), // ≤ 2007: adult on every day of 2026 (audit C1)
            Gender = gender,
            Position = position,
            HeightCm = height,
            City = city.Name,
            Region = city.Region,
            CurrentClub = currentClub,
            LookingForTeam = !hasClub || r.Double() < 0.15,
            Bio = string.Format(Pick(r, Bios), PositionBio[position], city.Name),
            VideoUrl = null,
            IsDemo = true,
        };

        AddSeasons(r, athlete, sameGender, currentClub);
        return athlete;
    }

    private static void AddSeasons(Randomizer r, Athlete athlete, List<Club> sameGender, Club? currentClub)
    {
        // Athletes without a current club have history only up to 2024-25.
        var available = currentClub is null ? Seasons.Take(2).ToArray() : Seasons;
        var count = r.Int(1, available.Length);

        foreach (var season in available.Skip(available.Length - count))
        {
            var isCurrent = currentClub is not null && season == Seasons[^1];
            var club = isCurrent ? currentClub! : Pick(r, sameGender);
            athlete.Seasons.Add(new AthleteSeason { Season = season, Club = club });
            athlete.Stats.Add(NewStatLine(r, athlete.Position, season, club.League));
        }
    }

    private static StatLine NewStatLine(Randomizer r, Position position, string season, League league)
    {
        var p = Profile[position];
        // Higher leagues: slightly bigger numbers.
        var scale = league switch { League.A1 => 1.25, League.A2 => 1.1, League.B => 1.0, _ => 0.9 };
        var minutes = Between(r, 8, 34) * (league == League.A1 ? 0.95 : 1.0);

        var points = Clamp(Spread(r, p.Pts * scale, 0.55), 0.5, 26);
        var rebounds = Clamp(Spread(r, p.Reb * scale, 0.55), 0.2, 13);
        var assists = Clamp(Spread(r, p.Ast * scale, 0.6), 0.1, 8.5);
        var steals = Clamp(Spread(r, p.Stl * scale, 0.5), 0, 3.5);
        var blocks = Clamp(Spread(r, p.Blk * scale, 0.6), 0, 3.5);

        return new StatLine
        {
            Season = season,
            League = league,
            Games = r.Int(10, 30),
            Minutes = D(minutes),
            Points = D(points),
            Rebounds = D(rebounds),
            Assists = D(assists),
            Steals = D(steals),
            Blocks = D(blocks),
            FgPct = D(Clamp(Between(r, 34, 56), 0, 99.9)),
            ThreePct = D(Clamp(Spread(r, p.Three, 0.25), 5, 45)),
            FtPct = D(Clamp(Between(r, 52, 90), 0, 99.9)),
            Source = SourceFor(r.Double()),
        };
    }

    private static StatSource SourceFor(double roll) =>
        roll < 0.60 ? StatSource.SelfReported
        : roll < 0.85 ? StatSource.CoachConfirmed
        : StatSource.LeagueVerifiedMock;

    private static T Pick<T>(Randomizer r, IReadOnlyList<T> list) => list[r.Int(0, list.Count - 1)];

    private static int WeightedIndex(Randomizer r, int[] weights)
    {
        var roll = r.Int(1, weights.Sum());
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return i;
        }
        return weights.Length - 1;
    }

    // Value around `centre` (+/- spread fraction), triangular-ish via two draws.
    private static double Spread(Randomizer r, double centre, double fraction) =>
        centre * (1 + fraction * (r.Double() + r.Double() - 1));

    private static double Between(Randomizer r, double min, double max) => min + (max - min) * r.Double();

    private static double Clamp(double v, double min, double max) => Math.Min(max, Math.Max(min, v));

    private static decimal D(double v) => Math.Round((decimal)v, 1);

    private static string UniqueSlug(string baseSlug, HashSet<string> used)
    {
        var slug = baseSlug;
        for (var n = 2; !used.Add(slug); n++) slug = $"{baseSlug}-{n}";
        return slug;
    }
}
