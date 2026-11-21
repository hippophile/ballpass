# fix-audit-findings (hardening after Clara's independent audit, 2026-11-21)

## Driver
Independent audit (Clara) of the 5 commits + uncommitted polish change: verdict PASS WITH FIXES —
1 critical, 6 major, 11 minor, each with measured evidence (queries, curl, a mutation test).

## Measured blast radius (2026-11-21)
| Finding | Evidence |
|---|---|
| C1 minors in demo data | 17 athletes born 2008 (17 years old for most of 2026); test used year difference |
| M1 vacuous interest test | mutant that never saves → 45/45 still green (seeded pair after persona reorder) |
| M2 open redirect bypass | `/culture/set?redirectUri=/%09/evil.example` → `Location: /\t/evil.example`; CRLF → blank 500 |
| M3 missing antiforgery | `POST /p/{slug}/shortlist` and `POST /demo/logout` succeed without token |
| M4 stale session after reset | 500 (FK violation) on interest/shortlist for a cookie whose coach id was deleted |
| M5 error paths | bad token → blank 500 (error handler crashes); 403/404 from POSTs re-executed as POST `/not-found` → empty 400 or wrong title |
| M6 demo script | README step 9 relies on luck to find the athlete on `/demo` |

## Decision
1. **C1:** birth years 1992–2007 (adult on every day of 2026). Test checks the worst case (birthday not yet passed).
   DEMO_DATA/README wording fixed.
2. **M1:** test picks a coach–athlete pair proven to have no interest, posts once → exactly 1, posts again → still 1,
   asserts the "already sent" text. `/demo` showcase order uses **seeded** interests only (stable during a demo).
3. **M2:** one shared `LocalRedirect.IsSafe(url)` helper (starts with `/`, not `//` or `/\`, no control characters),
   used by `/culture/set` and `/demo/login`; also applied when `/demo` echoes `ReturnUrl` into the forms.
4. **M3/M5:** every state-changing POST validates antiforgery explicitly via `IAntiforgery.ValidateRequestAsync`;
   invalid/missing token → **400** with a normal page (banner), never 500. Status-code pages and the error page accept
   re-executed POSTs (`[RequireAntiforgeryToken(required: false)]`); 403 renders the Denied page.
5. **M4:** cookie `OnValidatePrincipal` rejects the session when the persona row (`entity_id`) no longer exists →
   user is signed out and sent to `/demo` instead of a 500.
6. **M6:** README demo script names a fixed showcase flow (the first athlete on `/demo`, who has interests waiting);
   "sign in as that athlete" step rewritten to be deterministic.
7. **Minor fixes in scope:** m1 duplicate-submit race → unique-violation caught, no 500; m2 README "every row" wording;
   m3 Error page localized, no Development hint; m4 untrack `.playwright-cli`; m5 IsDemo filter on profile load;
   m6 search input labels (no overriding aria-label), NotFound uses h1; m9 home copy ("verified" overclaim, step 1
   wording, footer); m10 no seeded interest from a coach to their own club's player; m7 reset test also asserts
   interest/shortlist counts.
8. **Out of scope (recorded, not done):** m8 public base URL (deploy work), template leftovers in `_Imports.razor`
   and bootstrap JS (surgical-changes rule), SkeletonTests using the dev DB.

## Verification
- Red first: new failing tests for C1, M1, M2, M3, M4, M5, m1, m7.
- Green full suite; re-run Clara's mutation (interest never saved) → must now fail a test.
- Phone-width check repeated.

## Caveats
- Antiforgery validation is now explicit in each endpoint — a new endpoint must do the same (noted in README rules).
