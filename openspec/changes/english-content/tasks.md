# Tasks — english-content

- [x] 1. Red tests `tests/Ballpass.Web.Tests/EnglishContentTests.cs` (own host `EnglishContentAppFactory`, shipped config, DB `ballpass_encontent`)
      with minimal stubs (nullable *En columns + migration `EnglishContent`, `DisplayText` / `DisplayDate` / `Ordinal` / `HashtagCatalog`
      returning today's behaviour). Red run below.
- [x] 2. Seeders write English: clubs (`NameEn`), bios, interest notes, posts (text, title, place, positions), comments, hashtag edits,
      messages (+ notes copied on accept). No extra random draws (seed-42 data unchanged).
- [x] 3. City Latin names → English exonyms; rule parser accepts "Larissa".
- [x] 4. Rendering: `DisplayText` (EN column when present), `HashtagCatalog` (label per language, English alias → same key, parse + filter),
      `DisplayDate` (inbox, messages day separators + "Connected since", posts, profile rail, relative-time fallback), `Ordinal`
      (evidence panel, proof pills, top-quartile badge; EN resources "{0} percentile").
- [x] 5. Green: changed tests, then full suite once (`BALLPASS_TESTDB_SUFFIX=_en2 dotnet test --blame-hang-timeout 5m`), 0 warnings.
- [x] 6. Screenshots at 1512 (fresh browser, EN): /feed (coach), /messages thread (coach), /me/inbox (featured athlete), /p/{featured},
      /match?need=featured, /demo; /messages in EL. Read and fix.

## Red run
`BALLPASS_TESTDB_SUFFIX=_en2 dotnet test --filter "FullyQualifiedName~EnglishContentTests"` (2026-11-23, stubs in place, seeders and pages untouched)
```
  Failed Tag_aliases_resolve_to_one_key(typed: "#Stats", key: "στατιστικα")          Expected: "στατιστικα"
  Failed Tag_aliases_resolve_to_one_key(typed: "#lookingforteam", key: "ψαχνωομαδα")  Expected: "ψαχνωομαδα"
  Failed English_pages_show_no_greek_letters(path: "/messages", persona: "coach")      /messages: Greek in English page:
                                                                                       Θύελλα Περιστερίου
                                                                                       Τέλεια, ευχαριστώ! Τα λέμε την Τρίτη στο γήπεδο!
  Failed English_pages_show_no_greek_letters(path: "/demo", persona: "")               Aikaterini Fotopoulou · Μέλισσες Πάτρας · A1
  Failed English_pages_show_no_greek_letters(path: "/p/{featured}", persona: "")       Σμολ φόργουορντ από την πόλη Πειραιάς. Δουλεύει καθημερινά ...
  Failed English_pages_show_no_greek_letters(path: "/feed", persona: "coach")          Αναζητώ ομάδα που εμπιστεύεται νέους παίκτες και παίζει ομαδικά.
  Failed English_pages_show_no_greek_letters(path: "/me/inbox", persona: "athlete")    Coach · Δελφίνια Χανίων · A2 ·
  Failed English_pages_show_no_greek_letters(path: "/match?need=featured", ...)        Small forward · 179 cm · 28 years old · Ηχώ Πειραιά
  Failed English_pages_show_no_greek_letters(path: "/messages/{thread}", ...)          Θύελλα Περιστερίου
  Failed English_and_greek_tag_filter_the_same_posts                                   Assert.InRange() Failure ... Actual: 0
  Failed Ordinals_in_english(n: 82, expected: "82nd")                                  (and the 14 other ordinal cases)
  Failed Dates_in_english                                                              Expected: "Nov 22, 2026"
  Failed Trending_tags_have_the_same_keys_and_counts_in_both_languages                 Item: ("στατιστικα", "4", "στατιστικά") ... Match found
  Failed Message_seed_writes_english_bodies                                            note: no English for «Ψάχνουμε ενίσχυση για τον δεύτερο γύρο ...»
  Failed English_dates_use_one_format                                                  Item: "Monday 16 November" ... Pattern not found
  Failed Tag_labels_follow_the_language                                                Expected: "lookingforteam"
  Failed Runtime_content_shows_as_typed_next_to_english_seed                           Greek besides the typed message: Βορέηδες Βόλου ...
  Failed Greek_pages_keep_the_greek_seed                                               ArgumentNullException (BioEn is null: no English seeded yet)
  Failed Cities_use_english_exonyms                                                    Expected ["Athens", "Piraeus", ...]
  Failed Feed_and_engage_seeds_write_english                                           place: no English for «Γήπεδο ομάδας, Ρόδος»
  Failed Generator_writes_english_clubs_bios_and_notes                                 club: no English for «Ηχώ Πειραιά»
Failed!  - Failed:    35, Passed:    33, Skipped:     0, Total:    68
```
Passing at red, by design: Greek-unchanged guards (Greek ordinals / dates, `#ψάχνωομάδα` / `#tryouts` / unknown-tag keys) and the
rule-parser city cases (the old transliterations already parsed; "Larissa" needed the parser change once the exonym landed).

## Green run
- `EnglishContentTests`: 68/68 green; then 9 more page cases added (/, /why, anonymous /feed, athlete /feed and /messages, /me/sent,
  /shortlist, /search, season): 77/77, no further code change needed.
- Related suites (Engage, Feed, Messages, DemoData*, Profile, Evidence, AiSearch, EnglishContent): 365/365.
- Full suite `BALLPASS_TESTDB_SUFFIX=_en2 dotnet test --blame-hang-timeout 5m`: 789 passed, 1 failed:
  `SkeletonTests.Every_page_shows_one_footer_disclosure_and_no_banner("/")` → 500. Cause: `SkeletonTests` uses the unsuffixed default host,
  i.e. the shared dev DB `ballpass`, which is still at migration `20261123162028_Messages` (no `Clubs.NameEn`). Not migrated from here on
  purpose (shared DB). Same 5 SkeletonTests pointed at this worktree's own migrated dev DB: 5/5 passed. Goes green after `make reset`
  (the seeder migrates) on the shared dev DB.
- `dotnet build`: 0 Warning(s), 0 Error(s).
