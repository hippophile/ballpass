# wow-look (Phase A "Look" of the wow release; serves M0 skeleton, M1 profile, M2 search, M3 interest, M4 shortlist, M5 demo login, M6 Greek/English)

## Driver
Founder (2026-11-21): "LinkedIn for athletes", "dark and cool", "one very good MVP, not multiple".
`docs/research/DECISIONS.md` overrides `docs/research/wow-plan.md`: ONE dark theme, accent #FF6A2B, no light theme,
no accent variants, no theme switch endpoint. Phase A lands the tokens, fonts and restyle first, so Phase B
(Feed / Find / Evidence / Share) builds on them in parallel.

## Measured blast radius (2026-11-21)
| Item | Count | Command |
|---|---|---|
| Razor `class="... card ..."` usages (Bootstrap collision) | 10 (all in AthleteProfile.razor) | `grep -rEn 'class="[^"]*\b(card\|badge)[ "-]' src/Ballpass.Web/Components` |
| Scoped `.card`/`.badge` CSS rules | 10 (AthleteProfile.razor.css) | `grep -rEc '\.(card\|badge)\b' ...` |
| Hard-coded hex colours in own CSS | 118 | `grep -rEo '#[0-9a-fA-F]{3,6}\b' Components/*/*.css wwwroot/app.css` |
| Own CSS lines to restyle | 859 (app.css 322, MainLayout 172, Home 173, AthleteProfile 192) | `wc -l` |
| Pages/layout touched | 9 (MainLayout, Home, AthleteProfile, Search, DemoLogin, Shortlist, Inbox, Denied/NotFound/Error) | |
| Existing tests referencing `card`/`badge` class names | 0 (tests use `data-*`, `class="result"`, `class="demo-banner"`) | `grep -rn "card\|badge" tests` |
| Existing tests | 55 | must stay green |
| Resource keys changed | 0 (same demo-banner text; new keys only if needed) | |

## Decision
1. `wwwroot/css/bp-tokens.css`: exactly the dark `:root` block from the plan (no lime/violet/light rules, no `/theme/set`).
2. `wwwroot/css/bp-base.css`: global element styles, Bootstrap variable mapping `--bs-*` to tokens, `@font-face`,
   cards/buttons/inputs/badges, motion rules (all off under `prefers-reduced-motion`). Loaded AFTER bootstrap in `App.razor`.
3. `<html data-theme="dark" data-bs-theme="dark">` hard-coded (no cookie, no switch).
4. Fonts self-hosted under `wwwroot/fonts/` (Inter text, Roboto Condensed display), woff2, Greek + Latin, with OFL texts.
   No runtime CDN. Fallback to system-ui if a font cannot be obtained (reported, not faked).
5. Restyle all existing pages to the dark system. Own `card` and `badge` classes renamed to `bp-card` / `bp-badge`.
6. Source badges: inline-SVG icon (circle / check / shield) plus text, colour from `--bp-src-*`; the league badge text contains "mock".
7. Optional, cheap, progressive: CSS-only count-up on home counters, `@view-transition { navigation: auto; }`; both off under reduced motion.

## Verification
- Red first: `LookTests` (dark attributes on 8 routes + demo banner, no external hosts, WCAG contrast of tokens, source badges).
- Green: `BALLPASS_TESTDB_SUFFIX=_look dotnet test`, all 55 existing + new tests.
- Visual: Brave headless screenshots at 1280x900 and 390x844 of `/`, `/p/{slug}`, `/search` (coach), `/demo`; read by the implementer;
  `scrollWidth <= innerWidth` at 390; Greek capitals with tonos in a screenshot.

## Caveats
- Chart series tokens (Okabe-Ito) from the plan are NOT added here; they belong to the Evidence package, which must re-verify them on #0B0F14.
- OFL Reserved Font Name clauses: only a human can sign off; files are served under neutral names (`bp-text.woff2`, `bp-display.woff2`).
- View transition is Chromium-only progressive enhancement; the count-up needs CSS `@property` (Chromium/Safari 16.4+/Firefox 128+), otherwise the final value shows.
- Contrast is computed for token pairs only; the test cannot see every composed pair in page CSS (e.g. text on `--bp-s3`).
- `--bp-warn`/`--bp-danger` are not in the contrast test list requested; they are checked on bg/s1/s2 as extra info in the report only.
