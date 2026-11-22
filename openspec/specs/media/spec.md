# media Specification

## Purpose
TBD - created by archiving change wow-media. Update Purpose after archive.

## Requirements

### Requirement: Post media renders without layout shift and with an honest label

Every media item SHALL render with `width`/`height`, a localized `alt`, `loading="lazy"`, `decoding="async"` and a visible chip
"Εικόνα αρχείου · demo" (stock photo) or "Γραφικό · demo" (generated graphic). Anonymous, athlete and coach SHALL all see it.

#### Scenario: Photo markup
- **WHEN** a post with a photo is rendered
- **THEN** the `img` has width, height, non-empty alt, lazy loading and the stock chip is present

#### Scenario: Video markup
- **WHEN** a post with a video is rendered
- **THEN** the `video` has `muted playsinline preload="none"` and a `poster`, and no `autoplay` attribute
- **AND** it keeps native `controls`

#### Scenario: Carousel
- **WHEN** a post has 2-4 photos
- **THEN** they render in a scroll-snap list with one dot link per photo; a single photo has no dots

### Requirement: Assets are self-hosted, logged and small

Every media file SHALL live under `wwwroot/media`, be logged in `docs/ASSETS.md`, and the total SHALL stay within 40 MB.

#### Scenario: Seed files exist
- **WHEN** the seed is generated
- **THEN** every file referenced by seeded media exists under `wwwroot/media`

#### Scenario: Every file is documented
- **WHEN** ASSETS.md is read
- **THEN** every file under `wwwroot/media` appears in `docs/ASSETS.md`

#### Scenario: Budget
- **WHEN** the folder is measured
- **THEN** the total size of `wwwroot/media` is at most 40 MB

#### Scenario: No external URLs
- **WHEN** a page with media is rendered
- **THEN** no media `src`/`poster` is an http(s) URL

### Requirement: Seeded media is deterministic

The seed SHALL give about 60 % of highlight / free-text / tryout / looking-for-team posts get 1-4 items; tryout calls get a court or gym photo; highlights
get a video or a 2-4 photo carousel; about 40 athletes get a reel of 1-3 items. The same seed produces the same rows.

#### Scenario: Same seed, same rows
- **WHEN** the seed runs twice on the same posts and athletes
- **THEN** the media rows are identical

### Requirement: Stories bar and profile highlights

`/feed` SHALL show a stories bar of athletes that have a reel, and the profile SHALL show the reel in `#highlights` in place of the no-video placeholder.

#### Scenario: Stories bar
- **WHEN** `/feed` is requested
- **THEN** `/feed` shows "Στιγμιότυπα" with only athletes that have a reel, each linking to `/p/{slug}#highlights`

#### Scenario: Profile
- **WHEN** an athlete has a reel
- **THEN** `#highlights` shows it and "Δεν έχει προστεθεί βίντεο ακόμη" is absent; without a reel the placeholder stays

### Requirement: Jersey avatar

`JerseyAvatar` SHALL be deterministic (same id gives the same number 0-99 and colour), keep the initials as SVG text, use a neutral
colour without a club, and show a clipboard badge for coaches.

#### Scenario: Deterministic jersey
- **WHEN** the same id and club are rendered twice
- **THEN** number, colour and initials are identical, and the number text contrasts >= 4.5 with the fill
