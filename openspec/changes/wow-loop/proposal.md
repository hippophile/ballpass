# wow-loop (Phase C "Loop" of the wow release + 5 integration fixes)

## Driver
`docs/research/DECISIONS.md`: one-click persona switch, interest timeline (sent -> seen -> replied) with Accept/Decline, reset button.
No presenter bar, no pulse funnel, no club needs board.

## Measured blast radius (2026-11-22, branch wow-release + this change)
| Item | Count | Command |
|---|---|---|
| Entities | 1 changed (`Interest`: SeenAt, Reply, RepliedAt, ReplyReadAt) | `git diff --stat` |
| Migrations | 1 additive (`WowLoop`) | `dotnet ef migrations add WowLoop` |
| Shared files edited | MainLayout (2 lines), Home (1), DemoLogin (1 link), AthleteProfile (action bar), Inbox (extended), Search (pager), Season, Program.cs (2 lines), App.razor (1 css), appsettings | `git diff --stat` |
| Deleted | `Features/Share/SeasonCohort.cs` (orphan duplicate of ICohortRank) + its 2 tests | n/a |
| Existing tests | 167+ must stay green | `BALLPASS_TESTDB_SUFFIX=_loop dotnet test` |

## Decision
1. Integration fixes: Evidence minutes floor (`Evidence:MinMinutesPerGame`, default 15), Wrapped uses `ICohortRank`,
   compact coach action bar, composer hint once, search pager keeps `parsed`/`nlq`/`unsupported`.
2. Interest lifecycle: SeenAt (set when the athlete opens /me/inbox), Reply None/Accepted/Declined + RepliedAt,
   ReplyReadAt (coach opened /me/sent; drives the unread-replies count; small addition beyond the brief).
3. /me/sent (coach) with a 3-step stepper; POST /me/inbox/{id}/reply (athlete, owner only).
4. `Demo:Enabled` (default true): persona switch pill + /demo/reset (confirm page + POST).
5. Seed: 30% seen, 15% replied, deterministic, timestamps between CreatedAt and the reset time.
6. Accepted reply shows a fictional club contact `contact@<club-slug>.demo.invalid`, never a real address.
