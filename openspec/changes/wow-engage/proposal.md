# wow-engage (Phase D package ENGAGE: comments, public engagement, liveliness widgets)

## Driver
Founder feedback 2026-11-22: "something is missing ... people engaging, this is too stiff". Founder decisions: comments come back (they were cut
in `wow-feed` / DECISIONS.md) but with safeguards; the 🔥 count becomes public; who-to-follow, trending tags and a "new posts" pill make the feed feel alive.

## Reversal (documented on purpose)
`wow-feed` Requirement "Kudos ... visible as a count only to the post author" is REVERSED: the count is visible to everyone (anonymous included).
`DECISIONS.md` "NO comments" is REVERSED for comments only: still no replies-to-replies, no DMs, no mentions, no media in comments.

## Decision
1. Entities in `Data/EngageEntities.cs`: `Comment`, `CommentReport`, `PostTag` (cascade from Post). Migration `WowEngage`.
2. Services in `Features/Engage` behind `IEngageService`; page components catch failures and render nothing (feed isolation).
3. Comment rules, all server-side: <= 280 chars, URLs stripped (`Safeguards.Clean`), 1 comment / 20 s per persona (`Engage:CommentCooldownSeconds`),
   adults only, antiforgery 400, signed-out 403, blocked pairs cannot comment on each other's posts and do not see each other's comments,
   the comment author deletes, the post author hides (club posts: the posting head coach), anyone else reports; 3 distinct reports hide for everyone
   (`Feed:ReportHideThreshold`, same reason list as post reports).
4. Hashtags: `#word` (Greek/Latin letters, digits, `_`), max 3 per post, matched case- and accent-insensitively (`ΠΡΟΠΟΝΗΣΗ` = `προπόνηση`),
   indexed into `PostTag` when a post is created. `/feed?tag=` filters with the normal keyset paging.
5. Widgets (who-to-follow, trends, new-posts pill) are plain server-rendered components, no polling, no JS.
   "New posts" = posts newer than the `bp_lastvisit` cookie (default: 24 h before the first visit).
6. Seed (`Demo/EngageSeeder.cs`, pure function of data set + reference time): 150 comments, kudos raised from 45 to 250 (some posts 20-40),
   hashtags on ~40% of posts. Labelled on /feed: "Δείγμα δραστηριότητας (seeded)".

## Caveats
- Seeded comments and kudos are fictional demo activity, labelled as such.
- Trends are computed from the tag index on each request (demo scale; no cache).
- The new-posts pill links to `/feed` (which also resets the cookie), not to an in-page anchor.
