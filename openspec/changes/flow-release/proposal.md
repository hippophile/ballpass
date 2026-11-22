# flow-release (serves M0 skeleton, M1 profile, M2 search/match, M3 interest, M4 shortlist, M5 demo login, M6 Greek/English)

## Driver
Founder (2026-11-22): the product must read as one flow, "athlete raises her hand (teal), coach answers (orange), they connect",
and "don't say demo or image demo, we know it, it's killing the vibe". Design spec: `scratchpad/flow/SPEC.md` (lead), with
§8 LEAD OVERRIDES winning over the rest (O1 no demo labels, O2 formal Greek, O3 gender-correct copy, O4 drop one photo, O5 WP-A owns O1).

## Measured blast radius (2026-11-22, branch `wow-release` @ d1fd76e)
| Item | Count | Command |
|---|---|---|
| Visible "demo"/"δείγμα"/"seeded"/"fictional" label keys in resx (el + en) | 40 keys in 8 resource pairs | `grep -n -i -E "demo\|δείγμα\|επίδειξ\|seeded\|fictional\|φανταστ" src/Ballpass.Web/Resources/*.resx` |
| Razor/C# sites rendering those labels | 27 | `grep -rn -E "Chip_Demo\|Chip_Stock\|Badge_Demo\|Seeded_Label\|..." src/Ballpass.Web --include=*.razor --include=*.cs` |
| Club names carrying the " (demo)" suffix (seed 42) | 20 of 20 | `DemoDataGenerator.cs:175` |
| Existing tests asserting a demo label or the banner | 14 test methods in 11 files | `grep -n -i "demo-banner\|(demo)\|δείγμα επίδειξης\|seeded\|Εικόνα αρχείου" tests/` |
| Existing tests | 384 `[Fact]`/`[Theory]` attributes | `grep -rhoE "\[(Fact\|Theory)" tests \| wc -l` |
| New stock media files (SPEC §5 minus pexels-40732461) | 30 media files + credits.json | `ls wwwroot/media/stock` |
| Pages whose width changes with the shell (960 px → 1280 px page) | every page | `MainLayout.razor.css .content` |

## Decision
Three work packages, WP-A merges first; WP-B and WP-C then run in parallel worktrees. Program.cs is touched by nobody.

**WP-A Foundations (this implementer)**
1. Tokens: the §3 diff in `bp-tokens.css` (deeper surfaces, brighter text, teal athlete accent, gradients, glows, scrims, `--bp-w-page:1280px`).
2. `bp-base.css` primitives: `.bp-btn-primary` (orange gradient), `.bp-btn-athlete` (teal gradient), `.bp-btn-outline`, `.bp-btn-ghost`,
   `.bp-chip-athlete`, `.bp-card`, `.bp-photo` + `.bp-photo-scrim`, `.bp-stepper`, and the type scale.
3. Shell: one 1280 px page width, logo aligned with the content edge, coach nav = Ταίριασμα · Λίστα · Αποστολές (n), athlete nav =
   Εισερχόμενα (n) · Το προφίλ μου, everything else in a `<details class="bp-usermenu">` with a 32 px jersey avatar; the persona switch
   pill and EL|EN stay visible.
4. O1 across the whole app: no demo banner, no "(demo)" chips or club suffix, no stock/graphic "demo" chip, no "seeded" label, no
   "δείγμα επίδειξης" cohort labels, no "fictional contact" note. Exactly ONE disclosure remains: a 12 px `--bp-faint` footer line
   "Φανταστικά πρόσωπα και δεδομένα" / "Fictional people and data" (plus the `/demo` intro sentence).
5. Media: 30 stock files copied to `wwwroot/media/stock/` with published names, `credits.json`, `docs/ASSETS.md` rows;
   `Features/Media/StockMedia.cs` (slot constants + pools; pages never type file names), `StockPhoto.razor` (img + scrim, no chip),
   `HeroVideo.razor` (`<source media>` for ≥768 px without reduced motion, poster otherwise).
6. Config: `Demo:Featured` (CoachFirstName, CoachLastName, AthleteSlug, NeedQuery) with empty defaults in appsettings.json and the
   values in appsettings.Development.json. NeedQuery is filled by WP-B.

**WP-B Coach path** (later): `/match` with the featured need prefilled + `AthleteMatchCard`, profile cover/proof row/action rail, shortlist.
**WP-C Front door, athlete path, feed** (later): home hero video + two doors + connection band + live counters, `/demo` featured row,
inbox accept in teal, sent stepper, feed 3-column grid, not-found band.

## Verification
- Red first: LookTests (§3 contrast table ±0.01, scrim over white, banner absent, footer disclosure present, nav link counts, no demo
  words in visible text), StockMediaTests (slots, pools, files, credits, M2 no-face rule, HeroVideo/StockPhoto markup), SkeletonTests,
  and every existing test that asserted a label rewritten to assert its absence.
- Green: `BALLPASS_TESTDB_SUFFIX=_flowa dotnet test` full suite once at the end.
- Visual: screenshots at 1512 and 390 of `/`, `/demo`, `/feed`, `/match`, `/p/danai-bakali`, read by the implementer; content left
  edge = logo left edge (±1 px); `scrollWidth <= 390`.

## Caveats
- Real faces appear in coach/door/feed stock (Pexels licence, not endorsements). Faces never sit next to a named fictional athlete (M2,
  tested). Replace before any public launch (SPEC §7.1). `openspec/config.yaml` still lists "recognisable faces" as out of scope; the
  lead's spec supersedes it for this release.
- Removing the per-element labels leaves one footer line as the only disclosure on most pages; the OG share card, which travels
  without the footer, keeps a quiet "ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL PERSON" band (no "DEMO").
- The league-verified source badge still says "mock": it is a data-honesty label about verification, not a demo label.
