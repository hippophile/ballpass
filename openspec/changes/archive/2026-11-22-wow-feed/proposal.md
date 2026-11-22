# wow-feed (Phase B package FEED of the wow release; serves M3 express interest, M4 shortlist, M5 demo login, M6 Greek/English)

## Driver
Founder (2026-11-21): "LinkedIn for athletes". `docs/research/DECISIONS.md`: feed with 6 post types, kudos (author sees count),
follows (shortlist creates a follow), report/hide/block, 40-post seed, NO comments. Job of the feed: a coach reaches "express interest".

## Measured blast radius (2026-11-21, worktree at wow-release 41330a0)
| Item | Count | Command |
|---|---|---|
| Existing tests (`[Fact]`/`[Theory]` methods) | see tasks.md green run | `grep -c "\[Fact\]\|\[Theory\]" tests/Ballpass.Web.Tests/*.cs` |
| Shared files touched | 7: Program.cs (2 lines + 1 shortlist line), BallpassDbContext (1 line), DemoDataSeeder (1 line), MainLayout (2 nav lines), Home (1 line), AthleteProfile (1 line), App.razor (1 link), _Imports (1 using), appsettings.json (Feed section) | `git diff --stat` |
| New tables | 6: Posts, Kudos, Follows, Blocks, Reports, PostHides | migration `WowFeed` |
| Demo rows added | 40 posts, 45 kudos, follows (every head coach's shortlist + 3 per seeded athlete author) | `FeedSeeder` tests |

## Decision
1. Entities in `Data/FeedEntities.cs`; actors are keyed `athlete:{id}` / `coach:{id}` / `club:{id}` (same format as the login claim), no FK to persona tables so a demo reset cannot be blocked.
2. Services behind `IFeedService` / `IFollowService` (own folder `Features/Feed`), so tests can swap in a throwing implementation.
   Seam components catch failures and render nothing; the shortlist hook is exception-safe (`FollowSafeAsync`).
3. Ranking without ML: `SortAt = CreatedAt + boost` stored at write time (boost hours in `appsettings` section `Feed`), keyset on `(SortAt, Id)`,
   cursor `?before={ticks}.{id}.{shown}`, page 10, cap 60.
4. Minimal API POST endpoints (`/feed/post|kudos|follow|unfollow|report|hide|block`), explicit antiforgery (400), role checks (403), `LocalRedirect.IsSafe`.
5. Seed is a pure function of (demo data set, reference time); re-running shifts all timestamps to the reset time.

## Caveats
- Posts by a Club are seed-only (posted via the head coach); signed-in coaches post as themselves.
- No admin report list, no saved-search or coach-notification feed items, no "a coach noticed you" inbox line (not in this package's scope).
- All demo posts are fictional; every card carries the "(demo)" chip.
