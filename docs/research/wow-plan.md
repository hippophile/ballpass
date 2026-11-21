# Ballpass "wow" release: BUILD PLAN

**TLDR: Dark theme, a feed, ranked matches, AI search, share cards and a 60-second demo, all labelled fictional. It splits into 6 parallel packages of about 52 agent-days in total. All day counts are estimates, not measured.**

Caveats that change decisions:
- **Localhost links do not unfurl in WhatsApp or Viber.** Crawlers need a public HTTPS URL, so use a tunnel (for example cloudflared) for the share demo.
- **The core-loop forms stay plain POST.** `/p/{slug}/interest` and `/shortlist` are `MapPost` minimal APIs. Enhanced forms error against non-Blazor endpoints (Microsoft docs). Nothing in this plan adds `data-enhance` to them.
- **Stat entity field names are assumed.** Agents map to the real entities. Handedness and secondary position may not exist on `Athlete`, so check before building and add them in the seed if they are missing.
- **No source ties any feed or share mechanic to recruiting outcomes.** The pitch claim is "the loop is built and clickable", never "viral" or "traction".

---

## 1. Vision and the 60-second demo

- **Vision:** Ballpass is where a Greek club finds a women's basketball player. The coach says what she needs, sees ranked athletes with reasons and trust labels, and sends interest. The athlete sees it land.
- **Look:** dark, cool and fast, with a living feed and a shareable card. Every person, club and stat is fictional and labelled.
- **Honesty:** the wow comes from the mechanism, not from fake scale.

Spoken in the first 15 s: *"Everything you see is fictional demo data. I'm showing the mechanism, not traction."*

| Sec | Screen | What the audience sees | Package |
|---|---|---|---|
| 0-8 | `/` | Dark home, counters labelled "demo records", honesty strip | P1 |
| 8-20 | `/search` | The coach types "κέντρο Αττική κάτω των 22". Filter chips fill, results are ranked with fit and reasons, and an "AI-interpreted" label shows | P3 |
| 20-32 | `/p/{slug}` | Percentile bars, trust bar, source badges, season line | P4 |
| 32-40 | Express interest | One tap, no chat (existing) | existing |
| 40-50 | Persona switch to the athlete, then inbox | The interest lands, with a sent/seen/shortlisted/replied timeline | P6 |
| 50-60 | `/feed` and the share card | A looking-for-team post with an interest button, then the dark share card | P2, P5 |

**3-minute add-ons:** Season Wrapped, two-player compare, region map, network pulse.
**Fallback ladder:** Reset button, then `make demo-restore` (pg_dump), then a screen recording kept outside the app. The founder reviewed the offline-snapshot idea and cut it because it cannot show the live loop.

---

## 2. Design system

**Direction:** dark default, one accent at a time, and a light theme kept for usability and printing, not for polish.

### Tokens (`wwwroot/css/bp-tokens.css`, loaded after Bootstrap)
Contrast ratios were computed with the WCAG formula. Lime and violet were computed for this plan. Everything else comes from the dark-UI lane.

```css
:root,[data-theme="dark"]{
 --bp-bg:#0B0F14;--bp-s1:#121821;--bp-s2:#1A2330;--bp-s3:#243044;
 --bp-border:#2C3A50;--bp-border-ui:#5C6E8A;
 --bp-text:#E8EEF5;--bp-muted:#9AA8BB;
 --bp-accent:#FF6A2B;--bp-accent-contrast:#0B0F14;--bp-focus:#8AB4FF;
 --bp-success:#3DDC97;--bp-warn:#FFB020;--bp-danger:#FF5C6C;
 --bp-src-self:#A7B4C6;--bp-src-coach:#4FC3F7;--bp-src-league:#B794F6;
 --bp-r-sm:6px;--bp-r-md:12px;--bp-r-pill:999px;
 --bp-shadow:0 1px 2px rgb(0 0 0/.4),0 8px 24px rgb(0 0 0/.35);
 --bp-sp-1:4px;--bp-sp-2:8px;--bp-sp-3:12px;--bp-sp-4:16px;--bp-sp-5:24px;--bp-sp-6:32px;--bp-sp-7:48px;
 --bp-fs-xs:.75rem;--bp-fs-sm:.875rem;--bp-fs-md:1rem;--bp-fs-lg:1.25rem;--bp-fs-xl:1.75rem;--bp-fs-stat:3rem;
 --bp-font-text:"BP Text",system-ui,sans-serif;--bp-font-display:"BP Display","Arial Narrow",sans-serif;
 color-scheme:dark}
[data-theme="dark"][data-accent="lime"]{--bp-accent:#B6F23C}
[data-theme="dark"][data-accent="violet"]{--bp-accent:#A78BFA}
[data-theme="light"]{
 --bp-bg:#F4F6F9;--bp-s1:#FFF;--bp-s2:#EEF1F6;--bp-s3:#E3E8F0;
 --bp-border:#D5DCE6;--bp-border-ui:#6B7A90;--bp-text:#101828;--bp-muted:#4B5767;
 --bp-accent:#C2410C;--bp-accent-contrast:#FFF;--bp-focus:#1D4ED8;
 --bp-success:#047857;--bp-warn:#92400E;--bp-danger:#B91C1C;
 --bp-src-self:#475569;--bp-src-coach:#0369A1;--bp-src-league:#6D28D9;
 --bp-shadow:0 1px 2px rgb(16 24 40/.08),0 6px 18px rgb(16 24 40/.08);color-scheme:light}
[data-theme]{--bs-body-bg:var(--bp-bg);--bs-body-color:var(--bp-text);--bs-secondary-color:var(--bp-muted);
 --bs-tertiary-bg:var(--bp-s2);--bs-border-color:var(--bp-border);--bs-link-color:var(--bp-accent);
 --bs-emphasis-color:var(--bp-text);--bs-body-font-family:var(--bp-font-text)}
```

**Accent contrast (computed):**

| Accent | Hex | on bg | on s1 | on s2 | on s3 | Dark text on accent |
|---|---|---|---|---|---|---|
| Orange (default) | #FF6A2B | 6.73 | 6.24 | 5.54 | 4.65 | 6.73 |
| Lime | #B6F23C | 14.42 | 13.37 | 11.87 | 9.96 | 14.42 |
| Violet | #A78BFA | 7.06 | 6.55 | 5.81 | 4.88 | 7.06 |

- Buttons always use dark text on the accent. White on orange is only 2.86, so that pairing is forbidden.
- Lime and violet exist in dark only. Light has orange only. That is my scope cut, because the light-theme lime and violet variants are not computed.
- The research's blue variant is dropped. Lime and violet replace it.

**Contrast rules from computed failures:**
- Danger text never goes on s3 (4.43, fail).
- Control borders use `--bp-border-ui` on bg, s1 and s2 only (2.56 on s3, fail).
- `--bp-border` is decoration only (1.55 on s1).
- s3 is for hover and pressed fills.
- In light, no coloured text on s3 (accent 4.21, fail).

**Chart series tokens** (Okabe-Ito hues, computed against #0F141C, so the CI test must re-verify them on #0B0F14):

| Series | Hex | Shape |
|---|---|---|
| A | #56B4E9 | circle, solid |
| B | #F0E442 | diamond, dashed |
| C | #009E73 | square |
| D | #CC79A7 | triangle |
| E | #D55E00 | (vermillion) |

- The track is decorative, so every value is printed as text.
- Colour is never the only encoding. Markers, dashes and source glyphs carry the same information.
- Source badges carry an icon (circle, check or shield) plus text, and the league tier always says "mock".

### Fonts (free, self-hosted, OFL 1.1)

| Role | Family | File | Size (measured by research lane) |
|---|---|---|---|
| Display, numbers | Roboto Condensed, variable 500-800 | `bp-display.woff2` | 38.1 KB |
| Text | Inter, variable 400-700, opsz 14 | `bp-text.woff2` | 34.5 KB |

- Both subsets cover Latin and Greek with tonos (0 of 69 needed Greek glyphs missing). Barlow Condensed and Oswald have no Greek, so they are out.
- Ship `OFL-Inter.txt` and `OFL-RobotoCondensed.txt` and link them from `/about`.
- **Open risk:** a person must read both OFL texts for a Reserved Font Name clause. The files are served under neutral names. Only Inter's licence text was read.
- Check Greek capitals with tonos before anything else.

### Card and number styling
- **`.bp-card`:** s1 fill, 1px border, radius 12px, padding 16px. On hover the border becomes `--bp-border-ui` and the card lifts 2px.
- **`.bp-stat`:** display font 800, `tabular-nums`, 3rem, with a muted uppercase xs label above it.
- **`.bp-badge`, `.bp-pill`:** pill radius, s2 fill, text in the source colour, icon, xs size.
- **`.bp-btn`:** primary is accent fill with dark text. Secondary is transparent with `--bp-border-ui`. Minimum height 40px.
- **`.bp-input`:** 2px focus ring in `--bp-focus` with a 2px offset.
- **Rejected:** neon glow, gradient text, glass blur. The only effect allowed is a 1px accent hairline plus a soft hero vignette.

### Motion (every rule off under `prefers-reduced-motion`)

| Rule | Spec | Reduced motion |
|---|---|---|
| Hover lift | 150ms ease-out | none |
| Fade-in | 200ms ease-out | none |
| Page cross-fade (`@view-transition{navigation:auto}`) | 200ms | `animation-duration:0` |
| Count-up (CSS `@property` + counter) | 800ms ease-out | final value shown |

- Cross-document view transitions are Chromium-first. This is progressive enhancement. Test it in the demo browser.
- Page links get `data-enhance-nav="false"` so the transition can fire.
- Drop the view transition if it fights the theme switch.

---

## 3. Release plan: 6 packages, built in parallel

**Workflow per package:** one openspec change in `openspec/changes/<pkg>/`, then validate. Tests come first, with a real failing run shown, then code to green. Sonnet implements, Opus audits independently, and agents never commit. The orchestrator verifies and commits per phase. While working, run only the tests for what changed. The full 55-plus suite runs once as the merge gate.

**Rules that keep packages from colliding:**

| Rule | Detail |
|---|---|
| Contract frozen on day 0 | The token names in section 2, the `bp-` prefix, the `?src=` values (`og`, `card`, `story`, `wrapped`), `Features:*` flags in appsettings, and four interfaces. The interfaces are `IDemoSeeder{Order}`, `IStatAggregator` (per-40, source mix), `ICohortRank` and `IBadgeService`. P4 owns the latter three and ships stubs on day 1 so P5 can compile. |
| Resources | Each package owns its own `.resx` pair (Greek default plus English), following the existing localization pattern. |
| CSS | Each package owns one file, `wwwroot/css/bp-<pkg>.css`, using only `var(--bp-*)` tokens. |
| Existing files | Packages add new components. Each host page gets a single-line insertion, merged by the orchestrator. Seams are `Program.cs` (one `AddX()` and `MapX()` per package), the nav, a slot in `AthleteProfile.razor` and a slot in `Search.razor`. |
| Migrations | Each package ships entities plus an `IEntityTypeConfiguration`. The orchestrator generates the migrations one at a time in merge order, because the snapshot file conflicts. |
| Seeding | Each package registers an `IDemoSeeder`, deterministic with a fixed random seed. P6's reset runs them in order. |
| Merge gate | P1 lands the tokens and the `card`/`badge` rename first, about days 1-2 (estimate). The others start on new files immediately and rebase after. |
| Feature flags | Every new feature sits behind `Features:*`. A test forces the feed service to throw and asserts `/search` and `/p/x` still return 200. |

**Effort total:** P1 4.5 + P2 8.5 + P3 11 + P4 10.5 + P5 9 + P6 9 = **52.5 agent-days (estimate)**. The critical path is about 10.5 days (P4, or P3 split in two), plus about 1 day for the contract gate and merges. Agent speed is unknown, so treat these as relative sizes.

### P1 "Look" (4.5 d, must)
**Owns:** `wwwroot/css/bp-tokens.css`, `bp-base.css`, `bp-look.css`, `wwwroot/fonts/*`, `App.razor`, `Components/Layout/*`, `Components/Shared/{DemoStrip,SourceBadge,CountUp}.razor`, the `/theme/set` endpoint, `Resources/Shared.resx`, `tests/Look*`.

| Feature | Class | Est |
|---|---|---|
| Tokens, theme cookie `bp-theme` plus accent cookie, `data-theme` and `data-bs-theme`, Bootstrap in a CSS layer, `card`/`badge` renamed to `bp-*`, contrast test | must | 1.5 |
| Self-hosted fonts and OFL files | must | 0.5 |
| Honesty strip and "League-verified (mock)" labels | must | 0.5 |
| Source-label badges | must | 0.5 |
| CSS count-up on counters and profile numbers | should | 1 |
| Cross-document view transition | should | 0.5 |

**Data:** none.

**Acceptance (testable):**
- `GET /` with cookie `bp-theme=light` returns `data-theme=light` and `data-bs-theme=light`. An unknown cookie falls back to dark.
- `/theme/set` rejects non-local redirects (`LocalRedirect.IsSafe`).
- A CI test computes contrast for every token pair. It requires 4.5 or higher for text and 3 or higher for UI and chart series, on the real bg.
- The honesty strip is present on every route including error pages, and in the OG tags, via a test over all routes. It cannot be switched off in non-demo builds.
- Each source badge has an icon, text and 4.5 or higher contrast on s1 and s2 in both themes. The league badge always contains "mock".
- The 55 existing tests are green after the rename. Run `grep -rn "card\|badge" tests` first.
- A Greek pangram with tonos renders in uppercase in both fonts. No request goes to an external host.
- Reduced motion removes the count-up, the lift and the transition.
- Screenshots of home, search and profile are attached in dark and light, and the light theme looks the same as today.

### P2 "Feed" (8.5 d, must plus should)
**Owns:** `Components/Pages/Feed/*`, `Components/Feed/*`, `Services/Feed/*`, `Data/Entities/Social/*`, `Seed/FeedSeeder.cs`, `Resources/Feed.resx`, `bp-feed.css`, an admin report list at `/admin/reports`, `tests/Feed*`.

| Feature | Class | Est |
|---|---|---|
| Post model, post-type cards, author chip, relative time, "..." menu | must | 2 |
| Following and Discover tabs with keyset "Show more" (Discover first) | should | 1.5 |
| Kudos toggle plus follow (shortlist creates a follow) | should | 0.5 |
| Safeguards: report, hide, block, adult gate, rate limit | must | 1.5 |
| 40-post cold-start seed | must | 1 |
| Coach feed items: saved-search match first, shortlist update second | should | 2 |

**Data:** `Post`, `Reaction`, `Follow`, `Block`, `Report`, `SavedSearch`. Indexes are `Post(CreatedAt DESC, Id DESC)`, `Post(AuthorId, CreatedAt DESC)` and a unique `Reaction(PostId, ActorId)`. `SavedSearch` may not exist yet, and the 2 d includes adding it.

**Routes:** `/feed`, `/feed/discover`, `/feed/following`, `POST /feed/kudos`, `/feed/report`, `/feed/hide`, `/feed/block`. The seam is one nav link.

**Acceptance:**
- The six types render with type-specific fields, with JS disabled.
- Posting permissions are enforced server-side. Athletes post types 1, 3, 4 and 6. Coaches post types 2 and 6. Badges are system-only.
- Kudos is idempotent and requires an antiforgery token. A POST without one returns 400. Only the author sees the count.
- "Show more" uses a `?before=` cursor with no duplicates across pages and a cap of 60 posts.
- Hidden and blocked authors are excluded. A block removes both parties from each other's feeds and follows.
- An under-18 author is rejected server-side, in the seed and in a unit test.
- The seed matches the spec in section 4. Re-running it shifts all timestamps to "now".
- A forced feed exception leaves `/search` and `/p/x` at 200.
- Express interest on a feed card posts to the existing `/p/{slug}/interest` as a plain form.

**Build order if time is short:** four types first (looking-for-team, tryout, stat update, free text), then highlight and badge.

### P3 "Find" (11 d, two sub-lanes, two agents)
**Owns:** `Services/Fit/*`, `Services/Ai/*`, `Components/Search/*`, `Seed/FindSeeder.cs`, `Resources/Find.resx`, `bp-find.css`, `tests/Fit*`, `tests/Ai*`. Seam: `Search.razor` gets one line for the need-form slot and one for the NL box.

**P3a Match (6.5 d)**

| Feature | Class | Est |
|---|---|---|
| Fit engine, need form, ranked results, templated reasons (merges four candidates) | must | 3 |
| Stat trust bar plus "coach-confirmed only" search toggle | must | 1 |
| Coach daily interest quota ("3 of 5 left today", limit in appsettings, enforced server-side) | should | 1 |
| Greece region map: self-hosted public-domain SVG, links per region filter search | should | 1.5 |

- `FitScorer` is one pure deterministic class.
- Hard filters hide athletes and show "N hidden by filters". The soft-criteria weights are height .30, rebounds per 40 .30, region .15, age .15 and handedness .10. These are my estimates, shown as "default weights".
- Weights renormalise over the criteria the coach requested. Rebounds with fewer than 10 games count as missing.
- Tie-breaks run in this order: coverage, best source, recency, slug.
- To keep scope near 3 days, show a single score plus a "partial data" tag instead of the lower-upper range.
- Acceptance: unit tests reproduce the worked example (A 93, B 60, C 60 with partial data), B ranks above C by coverage, every reason carries a source label, and the UI never says "AI".
- Trust-bar percentages sum to 100 and match the data. The toggle filters results.
- The quota is enforced server-side, and the counter updates after each interest.
- Each region link filters search, and the map has a text fallback.

**P3b AI search (4.5 d, must)**

| Feature | Est |
|---|---|
| NL box to filters | 3 |
| Safety rail (config, cache, cap, kill switch) | 1.5 |

- The model returns schema-only JSON, with enum values validated and `minAge` clamped to 18 or higher.
- Anything with no matching field (handedness, salary) goes into an `unsupported` chip and does not change results.
- The fallback is a rule parser labelled "Parsed by rules (no AI model)".
- The box redirects to `/search?...`, so it works in static SSR.
- The key lives in user-secrets or the env var `Anthropic__ApiKey`. A test fails the build if a key pattern is found in the repo.
- Config lives under `Ai:*` (`Enabled`, `Model`, `TimeoutSeconds` 4, `MaxCallsPerSession` 20, `MonthlyCapUsd`).
- Acceptance:
  - The 10 test queries equal the expected filters, with LLM runs recorded as fixtures.
  - With `Ai:Enabled=false` the rule parser handles queries 1, 2, 5 and 9.
  - An identical repeat query makes zero API calls.
  - A 429 `enforced_spend_limit_reached` or a 400 user-limit response falls back without retry.
  - A timeout falls back to rules.
  - "Under 16" clamps to 18 and flags the query.
  - A prompt-injection query returns empty filters.
- Cost is about $0.16 per 1000 calls on the cheapest Haiku tier (estimate from list prices, not measured).
- LLM rewording of fit reasons is a later stretch, with a number post-check. It is not part of v1.

### P4 "Evidence" (10.5 d, must plus should)
**Owns:** `Components/Charts/*`, `Services/Stats/*` (`StatAggregator`, `CohortService`, `BadgeService`), `Components/Pages/{Leaderboard,Compare}.razor`, `Resources/Evidence.resx`, `bp-evidence.css`, `tests/Stats*`. Seam: an evidence slot in `AthleteProfile.razor`.

| Feature | Class | Est |
|---|---|---|
| `PercentileBar` with demo-cohort label | must | 2.5 |
| Season line plus per-game bars with a ▲/▼/▬ trend (one chart set) | should | 1.5 |
| Two-player compare from the shortlist | should | 2 |
| Demo-cohort leaderboard | should | 1.5 |
| Shot-zone half-court SVG, shown only when zone data exists | should | 1.5 |
| Rule-based badges (5-6 rules tied to source labels) | should | 1 |
| Profile completeness checklist (plain, not AI) | should | 0.5 |

**Data:** the percentile definition is mid-rank, clamped 1-99, per 40 minutes, within a position group. Shot zones need fictional per-zone attempt and make data for a subset of athletes only. Season points need multi-season stat lines, which may require a seed extension.

**Acceptance:**
- Percentile 78 draws 156 of 200 units.
- A cohort under 20 peers or an athlete under 10 games renders no fill and "Not enough data (n=…)".
- Every row has `role=img` with an aria-label and a table fallback. The three source glyphs differ by shape.
- The "confirmed only" toggle recomputes the cohort.
- A missing season breaks the line. The trend follows the ±10% rule and shows "n/a" under 8 games. The chart is legible at 320 px.
- In compare, markers differ by shape and dash, positions that differ show a cohort warning, and the table equals the SVG.
- The leaderboard heading reads "Demo cohort (fictional)", shows the excluded count, and breaks ties by games, then slug.
- Each badge text names its rule (for example "Coach-confirmed: 5 games"), and each rule has a unit test.
- All SVG numbers use `CultureInfo.InvariantCulture`. A test runs under the `el` culture and asserts `points=` has dots.
- The thresholds (20 peers, 10 games, ±10%, 8 games) are my estimates and are labelled so in the UI notes.

### P5 "Share" (9 d, must plus should)
**Owns:** `Services/Cards/*` (one shared Skia renderer, disk cache), `Components/Pages/{Season,SharePanel}.razor`, `/p/{slug}/{og,card,story}.png`, `/p/{slug}/season`, print CSS, `wwwroot/fonts/` reuse, `Resources/Share.resx`, `bp-share.css`, `tests/Share*`. Seam: `og:*` meta tags in the profile head, plus a share panel slot.

| Feature | Class | Est |
|---|---|---|
| OG card 1200x630 plus shared renderer | must | 2 |
| Season Wrapped, 6 slides first and 8 if time allows | must | 3 |
| Player card 1080x1350 plus story 1080x1920 with QR | should | 3 (story reuses the card template) |
| Printable one-page scout report, print CSS, demo watermark, no new library | should | 1 |

**Library:** SkiaSharp (MIT) plus QRCoder (MIT). ImageSharp is rejected: it uses a split licence and a licence key at build time. The Linux VM needs `SkiaSharp.NativeAssets.Linux.NoDependencies`. Pin one exact version, and run one render on the M1 to confirm macOS arm64 before relying on it. The tunnel or public base URL comes from appsettings, never hardcoded.

**Acceptance:**
- `/p/{slug}` has an absolute `og:image` URL, `og:image:width=1200`, `og:image:height=630` and `og:image:alt`. Endpoints are anonymous because crawlers send no cookie.
- The PNG is exactly 1200x630 and under 300 KB (estimate target). A repeat request returns 304 or is served from cache. Report the measured time, and do not guess it.
- The watermark uses solid top and bottom bands (at least 5% of height each) plus a tiled diagonal. A test crops the bottom-right 50% and the centre 50% and asserts the text is present.
- The card and story are exact sizes. The QR decodes to `https://{base}/p/{slug}?src=card` in a test. Five sample slugs render, including a long Greek name without overflow.
- Wrapped slides render without JS, every number equals its LINQ aggregate in a test, and the final slide links to `/p/{slug}?src=wrapped`. Under 5 games only slides 2, 3 and 6 show. The cohort rank slide is labelled "among 200 demo athletes" and comes from `ICohortRank`, stubbed until P4 merges.
- Concurrent renders are capped at 2. The cache is keyed by slug plus a content hash.

### P6 "Loop and Demo" (9 d, must plus should)
**Owns:** `Services/Demo/*` (persona switch, reset, presenter, funnel), `Data/Entities/Demo/ProfileView` and `ClubNeed`, `Components/Pages/{Inbox timeline,ClubNeeds,Pulse}.razor`, `Seed/DemoSeeder.cs` (the orchestrator that runs every `IDemoSeeder`), `Resources/Demo.resx`, `bp-demo.css`, a Makefile target, `tests/Demo*`. Seam: the inbox page (extended in place) and a header pill.

| Feature | Class | Est |
|---|---|---|
| One-click persona switch (Coach Maria / Athlete Eleni) | must | 1 |
| Idempotent reset | must | 1.5 |
| Seen-by plus reply status, with an Accept/Decline reply, plus interest timeline | must | 2.5 |
| Club needs board ("N clubs looking for your position") | should | 1.5 |
| Guided `?step=N` presenter bar | should | 1.5 |
| Network pulse funnel (seeded events) | should | 1 |

**Acceptance:**
- `POST /demo/switch?to=athlete` sets the cookie and redirects with no JS. The next request has the new persona. It is hidden unless `Demo:Enabled`, which defaults to off outside demo mode.
- Reset needs an antiforgery POST and shows a confirmation. It truncates demo tables only, and two runs give an identical DB state (test). Measure the time on 300 athletes and report it. The target is under 5 s, which is a target, not a measurement.
- `make demo-restore` restores a `pg_dump`. This replaces the cut offline snapshot.
- Seen-by is visible to the owner athlete only. Every entry names a (demo) coach and club, and an empty state renders. Entries come from existing `Interest` and `Shortlist` rows plus seeded `ProfileView` rows, and are labelled "Seeded activity".
- The timeline shows sent, seen, shortlisted and replied as a stepper with timestamps.
- The club needs board reuses `FitScorer` in reverse and carries "(demo)" labels.
- The presenter bar is behind its own flag, never appears in screenshots, shows the checklist (strip on, reset done, persona correct), and an invalid step returns step 1.
- The pulse funnel is an SVG from seeded events, headed "Seeded activity, not traction". No growth curves.

---

## 4. Feed spec

**Job of the feed:** coach discovery, not engagement. The goal is that a coach reaches "express interest". No source proves feeds raise recruiting, so the investor claim is only "updates keep coaches coming back to profiles".

| # | Post type | Who posts | Core fields | Loop link |
|---|---|---|---|---|
| 1 | Looking-for-team | Athlete (own) | position, level, region, availability date, 140-char note | View profile and Express interest buttons |
| 2 | Tryout call | Coach (own club) | club, date, place, positions, deadline | "I'm interested" opens the athlete's interest |
| 3 | Stat update | Athlete, or system on stat change | season, GP/PPG/RPG/APG, source label | Fresh numbers with a source badge |
| 4 | Highlight | Athlete | title, self-hosted placeholder art, tag | Placeholder art only, no real video |
| 5 | Badge | System only | rule badge, date | Trust signal (thin, cut early) |
| 6 | Free text | Athlete or coach | up to 280 chars, URLs stripped | Activity only (cut last of the types) |

Every card has an author chip, relative time, a "(demo)" chip on seeded items and a "..." menu (Report, Hide, Block).

**Seed (Greek default text):**

| Item | Rule |
|---|---|
| Volume | 40 posts from 25 authors (18 athletes, 5 coaches, 2 clubs) |
| Mix | 8 looking-for-team, 6 tryout, 10 stat update, 6 highlight, 4 badge, 6 free text |
| Timing | 6 in the last 24 h, 14 at 2-7 days, 20 at 8-30 days. Irregular gaps. Re-running shifts all to "now" |
| Kudos | 45 total, 15 posts with none, max 5 on one post |
| Labels | "(demo)" chip on each post plus the page banner. Fictional or seeded wording only |

**Reactions:** one reaction, Kudos. Only the author sees the count, because public counts with fictional data are engagement theatre. A coach's kudos adds an inbox line, "a coach noticed you". No other reaction types.

**Comments:** none. The founder reviewed them and cut them. They are chat by another name, they contradict "express interest, no chat", and they add moderation load. If added later, comments are coach-only, 280 characters, no replies, and the athlete can delete them.

**Follows:** Follow is a table used for ranking and is not shown as a follower count. Shortlisting creates a follow. A shortlisted author is pinned for 24 h.

**Ranking (no ML, weights in `appsettings`, my estimates):**
- **Following:** posts from followed or shortlisted authors, newest first. Boost looking-for-team and tryout posts. Boost authors matching a coach's saved search.
- **Discover:** all adult public posts by recency. A light boost for coach-confirmed and league-verified (mock). Maximum 2 consecutive posts from one author (skip this rule if time is short).

**Safeguards (hard requirements):**
- Adults only. The author's `BirthDate` must be 18 or older at seed and at signup. A post from an author under 18 is rejected server-side.
- Report with a reason list, and the reported user is not notified. The post is hidden from the reporter at once.
- 3 reports hide a post for everyone pending review (threshold is my estimate).
- Block and Hide are available. Every report is visible on an admin list for the demo.
- No DMs, URLs stripped from text, 1 post per author per minute, and length caps.
- Real moderation is out of scope for this POC, and the plan says so.

**Empty states:** a new user sees "Follow 3 athletes or save a search to fill your feed", with direct buttons.
**Pagination:** keyset cursor on `(CreatedAt, Id)` using `CreatedAt < c || (CreatedAt == c && Id < i)`, because tuple comparison may not translate in EF. Each re-render shows the top N, with a cap of 60.

---

## 5. Honest-demo rules

| Area | Rule (exact wording where given) |
|---|---|
| Strip, every page | `DEMO DATA · fictional adults · no real people` / `ΔΕΙΓΜΑΤΙΚΑ ΔΕΔΟΜΕΝΑ · φανταστικά πρόσωπα`. Tested over all routes and the OG tags |
| Entities | Club names end "(demo)". Seeded posts carry "(demo)" and "Seeded activity" |
| Verification | Chip `League-verified (mock)`, tooltip "Simulated. No league provided this." Never say "official", "certified" or "ranking list". No logos |
| Counters | "demo records", never "users" or "members". No invented likes, shares or "X coaches viewed this" |
| Seeded analytics | Seen-by, the funnel and the club needs board are labelled "Seeded activity, not traction" |
| Cohorts | Every percentile, leaderboard and rank says "demo cohort". Shown values keep their source label. Leaderboard heading reads "Demo cohort (fictional)" |
| Fit score | "Default weights", "computed on fictional demo data". Never called AI |
| AI search | EN "AI-interpreted search. Check the filters." / EL "Αναζήτηση με AI. Ελέγξτε τα φίλτρα." Filters stay editable chips. Unsupported asks show a "not filterable" chip |
| Rules path | EN "Generated from profile fields (rules)" / EL "Από πεδία προφίλ (κανόνες)". The rules path is never called AI |
| AI text, if added later | EN "AI-generated from profile data. Not a verified assessment." / EL "Παράγεται από AI με βάση δεδομένα προφίλ. Όχι επαληθευμένη αξιολόγηση." |
| Images | Watermark "DEMO - FICTIONAL ATHLETE - NOT REAL DATA" with a Greek line. Source label on the card. Generated vector avatars only, no photos and no AI faces. Crests are generated monograms with "(demo)" |
| Real-data switch | The watermark is driven per athlete by `IsDemo`, failing closed (default true). Real athletes would need an explicit consent flag. This is not built |
| Presenter bar | Behind its own flag, never in screenshots |
| AI privacy | Only fictional adult data reaches the API. Before any real data, a DPA, a GDPR basis and a human-review step are needed |

The EU AI Act Art. 50 notes come from the Commission FAQ, not the regulation text, and are not legal advice.

---

## 6. Risks and what we deliberately leave out

**Risks:**

| Risk | Mitigation |
|---|---|
| "Who verifies league-verified?" | The answer is "it's a mock in the POC". Real use needs a league partner. Add a short "how verification would work" note, with no partnership claim |
| Whole-page re-render per enhanced POST, and repeated form names need unique `@formname`s | Page size 10, unique names. The `FormMappingScope` API name is unverified, so the agent checks it |
| Enhanced nav may not patch `<html>` attributes, and may block view transitions | Theme and page links opt out of enhanced nav. Verify in Chrome |
| macOS arm64 and Linux Skia assets, and Greek glyph coverage in PNG | Render once on the M1, pin one Skia version, and test a Greek name with tonos |
| `card`/`badge` rename breaks string asserts | P1 greps the tests first and updates them first. It runs as the day-1 merge gate |
| Greek morphology in the rule parser is a rabbit hole | Small fixed vocabulary, and the LLM carries the rest |
| Leaderboards and percentiles look like real rankings | The demo cohort label sits in the heading, never only in the footer |
| Cohort of about 60 per position (research estimate) gives coarse percentiles | Minimum n of 20 and 10 games, raw values always shown |
| Several packages are large | Cut order below |
| Inter/Roboto Condensed reserved-name clause | A person reads both OFL texts |
| Investor asks traction, market size or monetisation | Say "none, no sourced figure, not built". The POC cannot answer these |

**Cut order if time is short:**
1. Blue/extra accent variants, then the count-up, then the view transition.
2. Feed comments (already cut), then Discover boosts, then the badge post, then highlight media.
3. Shot zones, leaderboard, completeness checklist, club needs board, pulse funnel.
4. Story export (card first), then the print report.

**Deliberately not built:**
- Chat or DMs, and comments.
- FUT-style overall rating or rarity tiers (an invented single score on real-looking athletes invites fake-rank criticism).
- Radar charts (area depends on axis order, and n is too small).
- Opaque "AI rating" numbers, auto highlights and live scores.
- Paid views as an upsell, crowd-sourced scout ratings and video export.
- Real photos, real clubs and real federation logos.
- The offline route-snapshot page (replaced by pg_dump restore).
- Any claim of growth, virality or market size.

---

## 7. Sources

- **Feed:** Strava Design (https://medium.com/strava-design/understanding-how-athletes-use-the-feed-aae986eaa818), Bikerumor on Strava favourites (https://bikerumor.com/2020/03/05/strava-gets-your-activity-feed-in-order-lets-you-play-favorites-with-followers/), Strava block/report (https://support.strava.com/en-us/articles/15402163-how-do-i-manage-my-followers-and-block-athletes-on-strava), LinkedIn report (https://www.linkedin.com/help/linkedin/answer/a526198/), NN/g empty states (https://www.nngroup.com/articles/empty-state-interface-design/), Hudl player profile and recruiting (https://www.hudl.com/support/athletes-guide-to-hudl/guides/player-profile, https://www.hudl.com/blog/athlete-share-hudl-recruiting).
- **Competitors:** NCSA App Store reviews (https://apps.apple.com/us/app/-/id673859348), FieldLevel (https://apps.apple.com/us/app/fieldlevel/id470231113), Scoutium (https://apps.apple.com/us/app/scoutium/id1412090548, developer copy, unverified), AOL on doctored film (https://www.aol.com/articles/high-school-players-using-ai-152206712.html).
- **Share:** Spotify Wrapped 2025 numbers (https://www.musicbusinessworldwide.com/spotify-wrapped-campaign-hit-200m-engaged-users-in-24-hours-a-19-yoy-increase, company-reported), Open Graph (https://ogp.me/), QRCoder (https://www.nuget.org/packages/QRCoder), SkiaSharp (https://www.nuget.org/packages/skiasharp), ImageSharp licence and pricing (https://sixlabors.com/pricing/, https://scancode-licensedb.aboutcode.org/six-labors-split-1.0.LICENSE).
- **Dataviz:** Opta radars (https://theanalyst.com/articles/introducing-opta-radars-compare-players), Minitab on radar charts (https://blog.minitab.com/en/blog/fun-with-statistics/beware-the-radar-chart), WCAG non-text contrast (https://www.w3.org/WAI/WCAG21/Understanding/non-text-contrast), Okabe-Ito palette (https://siegal.bio.nyu.edu/color-palette/), GitLab data-viz colour (https://design.gitlab.com/data-visualization/color).
- **Dark UI:** Roboto Condensed and Inter metadata (https://raw.githubusercontent.com/google/fonts/main/ofl/robotocondensed/METADATA.pb, https://raw.githubusercontent.com/google/fonts/main/ofl/inter/METADATA.pb), Bootstrap colour modes (https://getbootstrap.com/docs/5.3/customize/color-modes/), View Transitions (https://developer.chrome.com/blog/view-transitions-in-2025). Accent contrast for lime and violet was computed for this plan. Font sizes and Greek coverage were measured by the research lane with fontTools.
- **Demo:** Paul Graham (https://paulgraham.com/investors.html), YC demo-day guide (https://www.ycombinator.com/blog/guide-to-demo-day-pitches/), NextView (https://nextview.vc/blog/5-common-mistakes-founders-make-when-giving-demos-to-investors/, opening only, unverified).
- **AI:** Anthropic pricing (https://platform.claude.com/docs/en/about-claude/pricing), structured outputs (https://platform.claude.com/docs/en/build-with-claude/structured-outputs), rate limits (https://platform.claude.com/docs/en/api/rate-limits), EU Art. 50 FAQ (https://digital-strategy.ec.europa.eu/en/faqs/transparency-obligations-under-article-50-ai-act).
- **Tech:** Blazor forms (https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0), Blazor navigation (https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0).

Not sourced anywhere and labelled estimates in this plan: all effort days, all weights and thresholds, the 3-report hide rule, the 300 KB PNG target, the 5 s reset target, and the AI cost per 1000 calls.