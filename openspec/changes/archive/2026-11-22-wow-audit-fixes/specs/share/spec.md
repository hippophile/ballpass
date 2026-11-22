## ADDED Requirements

### Requirement: Card reflects looking-for-team

The share card's "Ψάχνει ομάδα" text SHALL follow the athlete's current `LookingForTeam`, and the card cache key SHALL change with it.

#### Scenario: Toggle
- **WHEN** an athlete without a club switches `LookingForTeam` off
- **THEN** the card model no longer contains "Ψάχνει ομάδα" and its hash and ETag differ

### Requirement: Wrapped note names the cohort

The wrapped cohort note SHALL mention the position group and the minutes floor.

#### Scenario: Note
- **WHEN** a forward's Season Wrapped shows the cohort slide
- **THEN** the note reads "...στην ίδια θέση (φόργουορντ), με τουλάχιστον 15 λεπτά ανά αγώνα..."

### Requirement: Full story boundary

Season Wrapped SHALL show the full story from exactly `FullStoryMinGames` (5) games.

#### Scenario: Exactly 5 games
- **WHEN** a season has 4, 5 or 6 games
- **THEN** only 5 and 6 give the full story
