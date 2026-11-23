namespace Ballpass.Web.Data;

public enum Gender { Female, Male }

public enum Position { PG, SG, SF, PF, C }

public enum League { A1, A2, B, Regional }

public enum StatSource { SelfReported, CoachConfirmed, LeagueVerifiedMock }

public enum CoachRole { Head, Assistant }

public class Club
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>English form (city exonym + translated nickname, "Larissa Halcyons"), seeded; shown in English.</summary>
    public string? NameEn { get; set; }
    public required string City { get; set; }
    public required string Region { get; set; }
    public Gender Gender { get; set; }
    public League League { get; set; }
    public bool IsDemo { get; set; }
}

public class Coach
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string FirstNameLatin { get; set; }
    public required string LastNameLatin { get; set; }
    public int ClubId { get; set; }
    public Club? Club { get; set; }
    public CoachRole Role { get; set; }
    public required string Region { get; set; }
    public bool IsDemo { get; set; }
}

public class Athlete
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string FirstNameLatin { get; set; }
    public required string LastNameLatin { get; set; }
    public int BirthYear { get; set; }
    public Gender Gender { get; set; }
    public Position Position { get; set; }
    public int HeightCm { get; set; }
    public required string City { get; set; }
    public required string Region { get; set; }
    public int? CurrentClubId { get; set; }
    public Club? CurrentClub { get; set; }
    public bool LookingForTeam { get; set; }
    public string? Bio { get; set; }
    /// <summary>English bio, filled only by the seeder; shown in English when present.</summary>
    public string? BioEn { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsDemo { get; set; }
    public List<AthleteSeason> Seasons { get; set; } = [];
    public List<StatLine> Stats { get; set; } = [];
}

public class AthleteSeason
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public int ClubId { get; set; }
    public Club? Club { get; set; }
    public required string Season { get; set; }
}

public class StatLine
{
    public int Id { get; set; }
    public int AthleteId { get; set; }
    public required string Season { get; set; }
    public League League { get; set; }
    public int Games { get; set; }
    public decimal Minutes { get; set; }
    public decimal Points { get; set; }
    public decimal Rebounds { get; set; }
    public decimal Assists { get; set; }
    public decimal Steals { get; set; }
    public decimal Blocks { get; set; }
    public decimal FgPct { get; set; }
    public decimal ThreePct { get; set; }
    public decimal FtPct { get; set; }
    public StatSource Source { get; set; }
}

public class Interest
{
    public int Id { get; set; }
    public int CoachId { get; set; }
    public Coach? Coach { get; set; }
    public int AthleteId { get; set; }
    public Athlete? Athlete { get; set; }
    public string? Note { get; set; }
    /// <summary>English note, filled only by the seeders; shown in English when present.</summary>
    public string? NoteEn { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Set once, when the athlete opens the inbox.</summary>
    public DateTimeOffset? SeenAt { get; set; }
    public InterestReply Reply { get; set; }
    public DateTimeOffset? RepliedAt { get; set; }
    /// <summary>Set when the coach opens the sent page after the reply; drives the unread-replies count.</summary>
    public DateTimeOffset? ReplyReadAt { get; set; }
    public bool IsDemo { get; set; }
}

public class ShortlistEntry
{
    public int Id { get; set; }
    public int CoachId { get; set; }
    public Coach? Coach { get; set; }
    public int AthleteId { get; set; }
    public Athlete? Athlete { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}
