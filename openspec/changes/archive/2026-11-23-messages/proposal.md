# messages (serves M3 express interest — the step after "accepted"; M5 demo login; M6 Greek/English)

## Driver
Founder (2026-11-23): "put fake messages etc and convos so i can see it better". Lead decision (2026-11-23): messaging is IN, but
ONLY between a coach and an athlete AFTER the athlete accepted that coach's interest — the connection IS the permission. This reverses
"no DMs" (DECISIONS.md, openspec/config.yaml).

## Measured blast radius (2026-11-23, branch `wow-release` @ f558b71)
| Item | Count | Command |
|---|---|---|
| Existing tests | 446 `[Fact]`/`[Theory]` attributes | `grep -rhoE "\[(Fact\|Theory)" tests \| wc -l` |
| Existing tests whose expectation changes | 5 methods in 3 files (nav link lists x2, inbox mailto class, reset counts, seeder interest count) | `LookTests`, `LoopTests`, `DemoDataSeederTests` |
| Shared files touched (one line per seam) | Program.cs (2), MainLayout.razor (2), App.razor (1), DemoDataSeeder.cs (ctor + 1 call), LoopSetup.cs (1), Inbox.razor (mailto class + 1), Sent.razor (1), AthleteProfile.razor (1) | `git diff --stat` |
| Seeded accepted interests before | 4 of 40 (seed 42) | `InterestLifecycleSeeder` |
| New tables | 3 (`Conversations`, `Messages`, `ConversationReports`), one migration `Messages` | `Data/MessagesEntities.cs` |

## Decision
1. Data: `Conversation` (one per accepted `Interest`: CoachId, AthleteId, unique InterestId, BlockedAt), `Message` (SenderKind
   coach|athlete, Body <= 1000, SentAt, ReadAt), `ConversationReport` (the athlete's report, reason, one per conversation).
2. Rule: a conversation exists only for an accepted interest; only its coach and athlete may read or write it (403 otherwise).
   Accepting an interest creates it, and a coach note becomes the first coach message. The athlete can report and block; a blocked
   conversation is closed (nobody can send).
3. Pages: `/messages` (two panes: list + preview of the latest thread) and `/messages/{id}` (thread opened, marks the other side's
   messages read). 390 px: list and thread are separate screens with a back link. PRG composer -> `#bottom`.
4. Entry points: nav "Μηνύματα (n)" for both roles, inbox accepted card "Στείλτε μήνυμα" (teal; the mailto stays as a ghost link),
   /me/sent accepted card and the profile rail "Μήνυμα", all through `/messages/with/{interestId}`.
5. Seed: deterministic (seed 42, reference time passed in): 25 conversations, 3-13 messages, last activity from minutes to two weeks
   ago, mixed unread; featured coach (Demo:Featured) >= 6 incl. 2 unread; featured athlete exactly 2 with other coaches.
   Extra accepted interests are added for them (the interest count rises from 40 to 40 + new).

## Verification
- Red first: MessagesTests (access, antiforgery, validation, block, report, read receipts, unread nav, seeding invariants,
  accept -> conversation, entry points) + the updated existing expectations.
- Green: `BALLPASS_TESTDB_SUFFIX=_msg dotnet test --blame-hang-timeout 5m`, 0 build warnings.
- Visual: screenshots 1512 + 390 read by the implementer; `scrollWidth <= 390`.

## Caveats
- Not real time: static SSR, the thread refreshes on navigation / send only (no push, no typing indicator).
- Block is final in this POC (no unblock UI); reports are stored, there is no moderation queue UI.
- Messages written during a demo are wiped by the demo reset like every other row.
