# Founder decisions on the wow plan (2026-11-21)

Founder direction: "LinkedIn for athletes", dark and cool, "put wow things in", "one very good MVP, not multiple".
Source research: `wow-plan.md` (20 agents, 8 research lanes + founder-critic). This file overrides it where they differ.

| Area | Decision |
|---|---|
| Theme | ONE dark theme. No light theme, no accent variants (lime/violet dropped). Accent = orange #FF6A2B. |
| Feed | 6 post types, kudos (author sees count), follows (shortlist creates follow), report/hide/block, 40-post seed. **No comments.** |
| Find | Fit engine + coach need form + ranked reasons; AI search (NL → filters) with rule fallback and safety rail. Region map: later. |
| Evidence | Percentile bars (demo cohort), season trend line, compare 2 players. No leaderboard, no radar, no overall rating, no shot zones. |
| Share | OG image card (SkiaSharp) + Season Wrapped. Story export and print report: later. |
| Loop | One-click persona switch, interest timeline (sent → seen → replied) with Accept/Decline, reset button. No presenter bar, no pulse funnel, no club needs board. |

Build order: Phase A Look (sequential) → Phase B Feed / Find / Evidence / Share in parallel worktrees → Phase C Loop + audit.

## Update 2026-11-22 — "alive feed" (founder: "something is missing, a feed photos images videos, people engaging, this is too stiff")

| Area | Decision (reverses earlier cuts where noted) |
|---|---|
| Photos & video | IN. Free-licence stock (Unsplash/Pexels/CC0) with **no recognisable faces**, self-hosted, compressed, every asset logged in docs/ASSETS.md; caption chip "stock · demo". Generated motion graphics allowed when labelled. |
| Comments | IN (reverses "No comments"). Flat, adults only, 280 chars, rate-limited, author deletes, post author hides, report → 3 reports hide, no replies-to-replies, no DMs. |
| Kudos count | PUBLIC (reverses "author only") + likers line. |
| Liveliness | Stories bar (highlight reels), jersey avatars (illustrated, no faces), who-to-follow, trending #tags, "new posts" pill. All seeded activity labelled. |
