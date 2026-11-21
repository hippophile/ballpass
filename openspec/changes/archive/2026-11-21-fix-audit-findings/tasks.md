# Tasks — fix-audit-findings

- [x] 1. Failing tests: `AuditFixTests` (M2, M3, M4, M5, m1), stricter `Adults_only` (C1), real interest test (M1), reset counts (m7). **Red run recorded.**
- [x] 2. (Zara) Security/robustness in Program.cs + pages: shared safe-redirect helper, explicit antiforgery validation → 400, OnValidatePrincipal, error/status pages accept re-executed POSTs, 403 → Denied, duplicate-insert race, Error page localized, IsDemo filter on profile, search labels a11y, NotFound h1, home copy (m9).
- [x] 3. (Orchestrator, in parallel) Generator: birth years ≤ 2007 (C1), no own-club seeded interests (m10); /demo order by seeded interests only (M1); README + DEMO_DATA wording (C1, m2, M6); untrack `.playwright-cli` (m4).
- [x] 4. Green full suite; re-run the audit mutation (interest never saved) → must fail a test.
- [x] 5. Mobile check + screenshots of changed pages.
- [x] 6. Validate, commit, push, archive (together with polish-home-and-mobile).

## Red run (task 1) — 2026-11-21
```
Failed DemoDataGeneratorTests.Adults_only
Failed AuditFixTests.Culture_switch_with_line_breaks_redirects_home_not_500
Failed AuditFixTests.Culture_switch_blocks_tab_bypass
Failed AuditFixTests.Shortlist_without_token_is_400
Failed AuditFixTests.Athlete_posting_shortlist_gets_403_denied_page_with_banner
Failed AuditFixTests.Login_return_url_with_control_characters_is_not_followed
Failed AuditFixTests.Parallel_duplicate_interests_store_one_and_never_500
Failed AuditFixTests.Logout_without_token_is_400_and_keeps_session
Failed AuditFixTests.Coach_session_after_reset_is_signed_out_not_500
Failed!  - Failed: 9, Passed: 45, Skipped: 0, Total: 54
```
The rewritten interest test passes because the feature works; its teeth are proven by the mutation run in task 4.

## Green run (task 4) — 2026-11-21
```
dotnet build → 0 Warning(s), 0 Error(s)
make test    → Passed!  - Failed: 0, Passed: 55, Skipped: 0, Total: 55
```

## Mutation re-run (audit M1)
Copy of the repo with the interest insert removed ("interest never saved"):
```
Failed InterestShortlistTests.Interest_sent_twice_appears_once_in_athlete_inbox
Failed AuditFixTests.Parallel_duplicate_interests_store_one_and_never_500
Failed!  - Failed: 2, Passed: 52, Total: 54
```
→ the mutant is now killed (it survived 45/45 before).

## Extra bug found while verifying (orchestrator)
Anonymous `POST /p/{slug}/shortlist` with no body → **500** ("Incorrect Content-Type"): the 400 was re-executed as
POST `/status/400` and the component endpoint tried to read a form. Red test `Anonymous_post_without_body_is_400_not_500`
(500 observed), fix: re-executed status/error pages run as GET. Green.

## DB checks after `make reset`
max birth year 2007 · own-club seeded interests 0 · duplicate names 0 · 40 interests · 74 shortlist entries.
Phone width 390px: all 8 pages 390/390.

## Build-gate incident
Mid-run the build broke (`RequireAntiforgeryToken` without its namespace while Zara was editing); orchestrator added
`@using Microsoft.AspNetCore.Antiforgery` to `_Imports.razor` and told Zara.

## Not done (out of scope, recorded)
Public base URL for share links (deploy), template leftovers in `_Imports.razor` / bootstrap JS, SkeletonTests on the dev DB.
