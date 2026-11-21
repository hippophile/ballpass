# wow-share (Phase B package SHARE; serves M1 athlete profile: shareable profile, wow release)

## Driver
`docs/research/DECISIONS.md` (2026-11-21): "Share = OG image card (SkiaSharp) + Season Wrapped. Story export and print report: later."
Player card (1080x1350) only if time remains after the two must items.

## Measured blast radius (2026-11-21, worktree based on wow-release 41330a0)
| Item | Count | Command |
|---|---|---|
| Existing tests | 71 (55 + 16 LookTests) | `grep -rEc "\[(Fact\|Theory)" tests` |
| Shared files edited | 4 (Program.cs 2 lines, AthleteProfile.razor 2 lines, App.razor 1 line, appsettings.json 1 key) | |
| New route | `/p/{slug}/og.png` (anonymous), `/p/{slug}/season` (public, no JS) | |
| New NuGet | SkiaSharp 4.157.0 + SkiaSharp.NativeAssets.Linux.NoDependencies 4.157.0 (MIT) | |
| New font files | 2 TTF (InterVariable 880 KB, RobotoCondensed VF 372 KB) in `Assets/fonts` (not wwwroot) + OFL texts | |

## Decision
1. `Features/Share/*`: Skia renderer (`IOgCardRenderer`), disk+memory cache keyed slug+content hash, 2 concurrent renders, ETag/304.
2. `<ShareMeta Athlete>` (og:image absolute via config `PublicBaseUrl`, fallback request base), `<SharePanel Athlete>` (preview, download, Wrapped link).
3. `Season.razor` at `/p/{slug}/season`: 6 CSS scroll-snap slides, no JS; cohort slide computed locally (TODO: swap for Evidence `ICohortRank` at merge).
4. Honest demo: card carries solid top/bottom bands "DEMO · ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL ATHLETE" plus tiled diagonal "DEMO".
