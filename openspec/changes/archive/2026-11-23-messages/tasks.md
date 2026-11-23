# Tasks — messages

- [x] 1. Docs: DECISIONS.md row (messaging IN after acceptance, reverses "no DMs"); openspec/config.yaml scope lines (chat/DMs out of
      the out-of-scope list with the rule; stale "recognisable faces" and "demo banner" items removed, founder 2026-11-22).
- [x] 2. Red tests (record the red run below): MessagesTests (access 403/anon, antiforgery 400, validation, no conversation without
      accepted interest, block stops coach, report stored, read receipts, unread nav counts, accept -> conversation + note, entry points,
      seeding invariants, resource formal register + English) and the updated expectations in LookTests / LoopTests / DemoDataSeederTests.
- [x] 3. Data: `Data/MessagesEntities.cs` (Conversation, Message, ConversationReport + IEntityTypeConfiguration), migration `Messages`.
- [x] 4. Feature: `Features/Messages/` (MessagesSetup AddMessages/MapMessages, MessagesService, pages, nav link, message button),
      `MessagesResource` el + en, `wwwroot/css/bp-messages.css`.
- [x] 5. Seams: Program.cs (2 lines), App.razor (1 link), MainLayout (2 nav lines), LoopSetup accept (1 line), Inbox / Sent / profile rail
      (1 line each + inbox mailto to ghost), DemoDataSeeder (options + 1 call).
- [x] 6. Seed: `Demo/MessagesSeeder.cs` deterministic (seed 42, reference time), featured personas from config.
- [x] 7. Green: affected tests, then the full suite once (`BALLPASS_TESTDB_SUFFIX=_msg dotnet test --blame-hang-timeout 5m`), 0 warnings.
- [x] 8. Screenshots (port 5304) read: /messages coach 1512 + 390 (list + thread), day separators + read receipts, after send,
      featured athlete, inbox accepted card; scrollWidth <= 390.

### Red run (task 2) — 2026-11-23
`BALLPASS_TESTDB_SUFFIX=_msg dotnet test --filter "MessagesTests|LookTests.Coach_nav|LookTests.Athlete_nav|LoopTests.Owner_accepts|LoopTests.Reset_requires|DemoDataSeederTests"`
(stubs only: entities, empty `MessagesSeeder.Build`, empty `AddMessages`/`MapMessages`; assertion failures, no compile errors):
```
Failed!  - Failed:    24, Passed:     1, Skipped:     0, Total:    25
  MessagesTests.Only_the_two_participants_can_read_a_thread            Assert.Equal() Failure: Values differ (404, expected 200)
  MessagesTests.Send_needs_antiforgery_and_a_participant               Assert.Equal() Failure: Values differ
  MessagesTests.Send_validates_the_body (x3)                           Assert.Equal() Failure: Values differ
  MessagesTests.Opening_a_thread_marks_the_other_side_read_...         Assert.Equal() Failure: Values differ
  MessagesTests.No_conversation_without_an_accepted_interest           Assert.Equal() Failure: Values differ
  MessagesTests.Accepting_creates_the_conversation_with_the_note_...   InvalidOperationException: Sequence contains no elements (no conversation)
  MessagesTests.Athlete_block_closes_the_conversation_for_the_coach    Assert.Equal() Failure: Values differ
  MessagesTests.Athlete_report_is_stored_once_with_its_reason          Assert.Matches() Failure: Pattern not found
  MessagesTests.Inbox_sent_and_profile_link_to_the_conversation_...    Assert.Matches() Failure: Pattern not found
  MessagesTests.Seed_is_deterministic_and_keeps_its_invariants         Assert.InRange() Failure: Value not in range (0)
  MessagesTests.Seeded_database_matches_the_build                      Assert.InRange() Failure: Value not in range
  MessagesTests.Messages_resource_is_formal_...                        MissingManifestResourceException (no resx yet)
  LookTests.Coach_nav_has_four_primary_links_and_a_user_menu           Assert.Equal() Failure: Collections differ
  LookTests.Athlete_nav_has_three_primary_links                        Assert.Equal() Failure: Values differ
  LoopTests.Owner_accepts_and_sees_the_fictional_club_contact          Assert.Matches() Failure: Pattern not found
  LoopTests.Reset_requires_antiforgery_..._idempotent                  Assert.InRange() Failure: Value not in range
  DemoDataSeederTests.Reset_twice_gives_exact_counts_not_doubled       Assert.True() Failure
  (+ List/Thread/Empty/Both_roles/Send_stores ...)
```

### Green — 2026-11-23
- Affected: `Passed! - Failed: 0, Passed: 25` (same filter), then 227/227 over Messages, Look, Loop, Home, FindPages, Profile, InterestShortlist,
  AuditFix, AuditMisc, DemoDataSeeder, SearchAndLogin.
- Full suite once: `BALLPASS_TESTDB_SUFFIX=_msg dotnet test --blame-hang-timeout 5m` -> `Passed! - Failed: 0, Passed: 637, Total: 637`.
- `dotnet build`: 0 warnings, 0 errors.

### Seed (seed 42, reset on ballpass_msg_dev)
61 interests (40 generated + 21 added, all accepted), 25 conversations (4 generated accepted + 21), featured coach 7 conversations
(2 unread), featured athlete 2 (3 after the 60-second accept, verified in the browser).

### Screenshots (port 5304, read)
coach-1512-list, coach-1512-thread, coach-1512-thread-read (day separators + "Διαβάστηκε"), coach-1512-after-send ("Εστάλη", list
reorders "μόλις τώρα"), coach-390-list, coach-390-thread (sticky head + composer, newest in view), athlete-1512-inbox ("Στείλτε μήνυμα"
teal + ghost email), athlete-1512-messages, athlete-1512-inbox-after-accept, athlete-1512-messages-after-accept, athlete-390-list,
coach-1512-sent ("Μήνυμα"), coach-1512-profile-rail ("Μήνυμα"). scrollWidth = 390 on /messages, /messages/1, /messages/10 at 390 px.
Fixed after reading: own bubbles from a muddy 20% tint to the role gradient with dark ink; singular unread texts.
