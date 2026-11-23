# media Specification

## Purpose
TBD - created by archiving change wow-media. Update Purpose after archive.

## Requirements

### Requirement: Post media renders without layout shift and with an honest label

Every media item SHALL render with `width`/`height`, a localized `alt`, `loading="lazy"` and `decoding="async"`, except the media of the first four posts of the first feed page, which load eagerly (above the fold). It SHALL NOT carry a visible "demo" chip (SPEC §8 O1); the alt text never claims to show the named athlete. Anonymous, athlete and coach SHALL all see the same markup. The feed SHALL never show an empty or generated-graphic tile: generated motion graphics (which carry a baked-in "ΓΡΑΦΙΚΟ · DEMO" label) are not shown in posts, and a highlight post, or a post whose only media were graphics, shows a stock photo from the feed pool picked by `postId % pool size`; a post that is text-only by design shows no tile.

#### Scenario: Photo markup
- **WHEN** a post with a photo is rendered below the first four posts
- **THEN** the `img` has width, height, non-empty alt and lazy loading, and no "Εικόνα αρχείου · demo" chip is present

#### Scenario: Above the fold
- **WHEN** the first four posts of the first feed page have media
- **THEN** their images have no `loading="lazy"`

#### Scenario: No empty or graphic tile
- **WHEN** every feed page is rendered
- **THEN** every media block contains an `img` with a `src` or a `video` with a `poster`, no generated graphic file is referenced, no generated court-art SVG is rendered
- **AND** each highlight post and each post whose media were only graphics shows the feed-pool stock photo for its id

#### Scenario: Video markup
- **WHEN** a video tile is rendered (athlete stories and highlights)
- **THEN** the `video` has `muted playsinline preload="none"` and a `poster`, and no `autoplay` attribute
- **AND** it keeps native `controls` and no "Γραφικό · demo" chip is present

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

### Requirement: Stock media slots

Stock media SHALL live under `wwwroot/media/stock/` with published names, be listed with source URL, author, licence and a `name` in `stock/credits.json`, and be referenced only through `Features/Media/StockMedia.cs` slot constants and pools. Slots shown next to a named fictional athlete (profile cover pool, highlights, match top card) SHALL use only files whose credits entry has no faces.

#### Scenario: Every slot resolves to a credited file
- **WHEN** every `StockMedia` constant and pool entry is resolved
- **THEN** the file exists under `wwwroot/media/stock/` and has a `credits.json` entry with `source_page_url`, `author`, `licence` and `name`

#### Scenario: No faces next to named athletes
- **WHEN** the cover pool, the highlight slots and the match top-card poster are checked against `credits.json`
- **THEN** every one has `faces: false`

#### Scenario: Excluded file never ships
- **WHEN** the stock folder is listed
- **THEN** it contains no file derived from `pexels-40732461` and the feed post pool has 10 entries

### Requirement: Stock photo and hero video components

`StockPhoto` SHALL render an `img` (width, height, `loading="lazy"`, `decoding="async"`, or `fetchpriority="high"` when marked as priority) plus a scrim layer and no chip. `HeroVideo` SHALL render `<video muted autoplay loop playsinline preload="metadata" poster=…>` whose only `<source>` carries `media="(min-width:768px) and (prefers-reduced-motion:no-preference)"`.

#### Scenario: Hero video only on wide screens without reduced motion
- **WHEN** `HeroVideo` is rendered
- **THEN** the `source` has that media query and the poster is the hero poster
