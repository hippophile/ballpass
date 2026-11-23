# Tasks — english-default

- [x] 1. Red tests `tests/Ballpass.Web.Tests/EnglishDefaultTests.cs` (record the red run below); pin the shared test host to `el`.
- [x] 2. `Localization:DefaultCulture` in appsettings.json, read in Program.cs (cookie provider only); toggle order EN | EL.
- [x] 3. English copy pass over every `*.en.resx` (180 values changed; 41 most visible before → after below).
- [x] 4. Green: changed tests, then full suite (`BALLPASS_TESTDB_SUFFIX=_en dotnet test --blame-hang-timeout 5m`), 0 warnings.
- [x] 5. Screenshots (fresh browser, no cookie) 1512: /, /why, /demo, /feed, /match?need=featured, /p/{featured}, /messages, /me/inbox; 390: /. Read and fix.

## Red run
`BALLPASS_TESTDB_SUFFIX=_en dotnet test --filter "FullyQualifiedName~EnglishDefaultTests"` (2026-11-23, shared host already pinned to `el`,
before the Program.cs / appsettings / toggle / copy changes). `DefaultCultureAppFactory` = shipped config on its own DB `ballpass_endefault`.
```
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/", persona: null, acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/why", persona: null, acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/demo", persona: null, acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/feed", persona: "coach", acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/match?need=featured", persona: "coach", acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/p/{featured}", persona: null, acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/messages", persona: "coach", acceptLanguage: null)
  Failed Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(path: "/me/inbox", persona: "athlete", acceptLanguage: null)
  ... (+16: the same 8 pages with Accept-Language "el" and "el-GR,el;q=0.9")
  Failed English_has_no_demo_words_outside_the_disclosures(resource: "SharedResource")
  Failed Language_toggle_lists_EN_before_EL
  Failed Shipped_default_culture_is_english
Failed!  - Failed:    27, Passed:    34, Skipped:     0, Total:    61
```
Every page failure: `<html lang> is not en`. Toggle: `Expected ["en","el"]`. Demo words: `Home_CtaSample: «See a sample athlete profile»`.
Passing at red, by design (they guard the copy pass): every key in both languages (11 resources), no Greek letters in English,
the toggle cookie switches to Greek.

## Green run
- `EnglishDefaultTests`: 61/61 passed.
- Test edits besides the `el` pin: `FindPagesTests` asserted the old English "AI-interpreted search. Check the filters." (copy changed);
  `SkeletonTests` footer test runs an unpinned host, so it now expects the shipped English footer.
- Full suite (first run): 711 passed, 2 failed (the two `SkeletonTests` footer cases above, fixed). Final run:
  `Passed!  - Failed: 0, Passed: 713, Skipped: 0, Total: 713` (0 warnings in the test output).
- `dotnet build`: 0 Warning(s), 0 Error(s).

## Before → after (41 most visible English strings)
| # | Key | Before | After |
|---|---|---|---|
| 1 | `Flow:Home_Title1` | Players find a team. | Athletes find teams. |
| 2 | `Flow:Home_Title2` | Teams find players. | Teams find athletes. |
| 3 | `Flow:Home_Sub` | One profile with proof. One expression of interest. One contact. | One profile with proof. One interest sent. One connection. |
| 4 | `Flow:Door_AthleteLabel` | For players | For athletes |
| 5 | `Flow:Door_AthleteCta` | See who wants you → | See who's interested → |
| 6 | `Flow:Door_CoachText` | Say what you need. We show who fits, and why. | Tell us what you need. We'll show who fits, and why. |
| 7 | `Flow:Door_AthleteText` | Clubs find you. You decide who to answer. | Clubs find you. You decide who to reply to. |
| 8 | `Flow:How_Title` | How the connection happens | How you connect |
| 9 | `Flow:How_Step1` | The player marks herself “Looking for a team” | The athlete turns on “Looking for a team” |
| 10 | `Flow:How_Step3` | She accepts; they exchange contacts | She accepts. They swap contact details |
| 11 | `Flow:Final_Title` | Start from your side | Pick your side |
| 12 | `Flow:Demo_AsCoach` | Enter as a coach | Continue as a coach |
| 13 | `Flow:Demo_AsAthlete` | Enter as a player | Continue as an athlete |
| 14 | `Flow:Inbox_Sub` | Coaches who are interested in you | Coaches interested in you |
| 15 | `Flow:Inbox_Connected` | You're connected ✓ | Connected ✓ |
| 16 | `Flow:Inbox_Email` | Send an email | Send email |
| 17 | `Flow:Count_Interests` | expressions of interest | interests sent |
| 18 | `Flow:Rail_AnonTitle` | Players ↔ coaches | Athletes ↔ coaches |
| 19 | `Shared:Interest_Submit` | Express interest | Send interest |
| 20 | `Feed:Interest` | Express interest | Send interest |
| 21 | `Feed:Interest_Sent` | ✓ Interest already sent | ✓ Interest sent |
| 22 | `Shared:Badge_LookingForTeam` | Looking for team | Looking for a team |
| 23 | `Shared:Nav_Logout` | Log out | Sign out |
| 24 | `Shared:Home_Value` | Where athletes meet coaches — on data with a clear source label, not connections. | Where athletes and coaches connect — on stats with a clear source, not on who you know. |
| 25 | `Shared:Status_interest-sent` | Your interest was sent. | Interest sent. |
| 26 | `Shared:Status_shortlist-added` | The athlete was added to your shortlist. | Added to your shortlist. |
| 27 | `Shared:Demo_Intro` | Pick a fictional person to try the platform. No password needed. | Pick a fictional person and try Ballpass. No password needed. |
| 28 | `Find:MatchLink_Cta` | Player match | Match players |
| 29 | `Find:Results_None` | No player passes the filters. | No players match these filters. |
| 30 | `Find:Weights_Heading` | How it is calculated | How the score works |
| 31 | `Find:Reasons_Heading` | Why it fits | Why it's a fit |
| 32 | `Find:Shortlist_EmptyCta` | Find athletes → | Find a player → |
| 33 | `Find:Shortlist_Count` | {0} on the list | {0} shortlisted |
| 34 | `Feed:Preview_Title` | What's happening now | Happening now |
| 35 | `Feed:Status_posted` | Your post is published. | Your post is live. |
| 36 | `Evidence:Cohort_Label` | comparison group: {0} {1} | compared with {0} {1} |
| 37 | `Evidence:Pct_Value` | {0}th percentile | Percentile {0} |
| 38 | `Why:How_Title` | Three moves. One connection. | Three steps. One connection. |
| 39 | `Why:Door_AthleteCta` | See who wants you → | See who's interested → |
| 40 | `Messages:Rule` | Messages open only after an interest is accepted. | Messaging opens once interest is accepted. |
| 41 | `Why:Frag_PdfFile` | womens_league_stats.pdf | womens_stats.pdf |

`Pct_Value` / `Badge_top-quartile`: "{0}th" was wrong for 81, 82, 83, 91, ... ("81th"); "Percentile {0}" matches the existing `Proof_Pct` pattern.

## Screenshots
App on :5306 (DB ballpass_en_dev, reset first), Brave headless, fresh session with no culture cookie; `scrollWidth == innerWidth` on every page.
Files in scratchpad/en/: home-1512(-full), why-1512-full, why-1512-v2-full, demo-1512-full, match-1512-full, feed-1512(-full),
profile-1512-full, messages-1512, messages-1512-v2, inbox-before-1512-full, home-390(-full). All read.
Fixed after reading: /why step-1 preview showed the Greek city (now the seed list's Latin name, same rule as the profile); the PDF
fragment file name was truncated (`womens_stats.pdf`); the /messages rule wrapped to two lines (shortened).

## Greek still visible in English (by decision)
- User content: posts, comments, hashtags, highlight titles, messages, interest notes, athlete bios (seeded, "written by" the personas).
- Club names (`Club.Name`, no Latin field): match/search cards, /demo rows, inbox, messages, feed author lines, profile club history.
- Share-card PNG / OG image: fixed Greek by design (one image for every crawler, `ShareText`).
