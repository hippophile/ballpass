# Tasks — add-interest-and-shortlist (M3, M4)

- [x] 1. Failing tests `InterestShortlistTests` + generator seed-count test. **Record red run.**
- [x] 2. Entities, mapping, migration; generator + seeder extended (deterministic).
- [x] 3. Profile actions (interest form, shortlist toggle) + POST endpoints with role checks and validation.
- [x] 4. `/shortlist` and `/me/inbox` pages; top bar links; resources el/en.
- [x] 5. Green: full `make test`. Record output.
- [x] 6. Screenshots: profile as coach, shortlist, inbox.
- [x] 7. Validate, commit, push, archive.

## Red run (task 1) — 2026-11-21

Stubs: `Interest`, `ShortlistEntry` entities (not mapped yet); `DemoDataSet.Interests/Shortlist` empty.
```
Failed InterestShortlistTests.Athlete_cannot_express_interest
Failed InterestShortlistTests.Coach_profile_has_interest_and_shortlist_actions
Failed InterestShortlistTests.Interest_sent_twice_appears_once_in_athlete_inbox
Failed InterestShortlistTests.Note_over_280_chars_is_rejected
Failed InterestShortlistTests.Shortlist_requires_coach
Failed InterestShortlistTests.Generator_seeds_40_interests_and_3_to_5_shortlist_entries_per_head_coach
Failed InterestShortlistTests.Anonymous_profile_has_no_actions
Failed InterestShortlistTests.Shortlist_toggle_adds_then_removes
Failed!  - Failed: 8, Passed: 34, Skipped: 0, Total: 42
```

## Green run (task 5) — 2026-11-21

```
dotnet build → 0 Warning(s), 0 Error(s)
make test    → Passed!  - Failed: 0, Passed: 42, Skipped: 0, Total: 42
make reset   → 20 clubs, 30 coaches, 300 athletes, 564 seasons, 564 stat lines, 40 interests, 74 shortlist entries
```

## Browser walkthrough (task 6)
Coach signs in → profile → sends interest with note → "Το ενδιαφέρον σας στάλθηκε." → adds to list → /shortlist shows
the athlete (4 entries: 3 seeded + 1) → logout → that athlete signs in → "Εισερχόμενα (1)" → inbox shows coach,
club, date and the note.

## Review fixes (orchestrator)
- After sending, the profile showed "already sent" AND the empty form → form now replaced by a ✓ confirmation.
  Spec scenario reworded ("posts ... twice"); test helper takes any antiforgery token on the page.
- `Coach_profile_has_interest_and_shortlist_actions` used the same coach/athlete pair as the duplicate test →
  order-dependent once the form disappears; moved to pair #3/#3.
- xUnit2013 warning in my test (`Assert.Equal(1, ...Count)`) → `Assert.Single`.

## Accepted deviations (Zara)
- Cookie `OnRedirectToLogin` emits a relative Location.
- Layout inbox count uses its own DI scope (avoids concurrent use of the request DbContext).
