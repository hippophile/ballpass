# add-interest-and-shortlist (MVP items M3 express interest, M4 shortlist)

## Driver
Founder: "build the mvp with all the fake user and data we discussed, dont stop until done" (2026-11-21).

## Why now
Last two MVP must-haves. Contact is deliberately **"express interest"**, not chat/DMs (analysis §2 gap 5, §5:
transfer/release rules, safeguarding; DMs are out of scope in openspec/config.yaml).

## Measured blast radius (2026-11-21)
| Item | Count |
|---|---|
| Entities | 5 (Club, Coach, Athlete, AthleteSeason, StatLine) |
| Pages that become action-aware | 1 (`/p/{slug}`) |
| New pages | 2 (`/shortlist` coach, `/me/inbox` athlete) |

## Decision
1. Entities `Interest` (CoachId, AthleteId, Note ≤ 280 chars, CreatedAt, IsDemo) and `ShortlistEntry`
   (CoachId, AthleteId, CreatedAt, IsDemo) with a unique index on (CoachId, AthleteId) for both. Migration.
2. Seed (deterministic, seed 42): each of the 20 head coaches shortlists 3–5 women athletes; 40 interests in total
   with short neutral Greek notes. Reset deletes these rows first.
3. Profile `/p/{slug}` for a signed-in coach: "express interest" form (optional note, antiforgery) →
   `POST /p/{slug}/interest`; shortlist toggle → `POST /p/{slug}/shortlist`. Both redirect back to the profile
   with a localized status message. Anonymous visitors and athletes see no action buttons.
4. One interest per coach–athlete pair: a repeat shows "already sent" and creates no row. Note > 280 chars is rejected.
5. `/shortlist` (CoachOnly): saved athletes as cards with a remove button.
6. `/me/inbox` (athlete role): interests received — coach name, club, date, note. Read-only; no reply/chat.
   Top bar: "Shortlist" for coaches, "Inbox (N)" for athletes.
7. Endpoints enforce roles server-side: a non-coach posting to `/p/{slug}/interest` or `/shortlist` gets a plain
   **403** (not a redirect).
8. Markup contracts (tests): profile forms `<form method="post" action="/p/{slug}/interest" data-action="interest">`
   (textarea `note`) and `... data-action="shortlist"`; `/shortlist` cards `<article class="result" data-slug="...">`;
   inbox items `<li class="interest" data-coach="{coach display name}">`; athlete persona forms on `/demo` also carry
   `data-slug="{slug}"`.
9. `DemoDataSet` gains `Interests` and `Shortlist` lists (entities with Coach/Athlete navigation).

## Verification
- Red first; tests act through the real forms (antiforgery).
- Interest appears in the target athlete's inbox exactly once even if sent twice.
- Shortlist add then remove; 281-char note rejected; athlete POST → 403; anonymous sees no buttons.
- Generator: seeded shortlist/interest counts and determinism.

## Caveats
- No notifications (email/push) — the athlete sees interest only when signed in. Fine for a demo.
- Interest notes are free text from coaches; a real pilot needs moderation/reporting (DSA notice-and-action, §5).
