# english-content (serves M6 Greek/English)

## Driver
Founder (2026-11-23): "the english language in most of the things are better for this kinda app". Lead decision: the investor demo
must read fully in the chosen language. In EN the screens still showed Greek seeded posts, comments, hashtags, highlight titles, bios,
interest notes and messages, Greek club names, transliterated cities ("Athina", "Peiraias", "Irakleio"), mixed date formats
("11/22/2026" vs "Sunday 1 November") and "Percentile 82".

Replaces the english-default out-of-scope line "Translating seeded user content": all of it is fictional seeded data, so the seeders
write both languages. Content typed at runtime (new posts, comments, messages, notes) has no English column and shows as typed.

## Decision
1. Nullable English columns, filled only by the seeders (one migration `EnglishContent`): `Post.TextEn/TitleEn/PlaceEn/PositionsEn`,
   `Comment.TextEn`, `Interest.NoteEn`, `Message.BodyEn`, `Athlete.BioEn`, `Club.NameEn`. EN shows the English column when present,
   otherwise the original. EL never reads them. Accepting an interest copies `NoteEn` into the first message's `BodyEn`.
2. Hashtags: `PostTag.Tag` stays the canonical key (the folded Greek key for seeded tags, so EL is unchanged). A small catalog maps a
   canonical key to its EL / EN label (`ψαχνωομαδα` = #ψάχνωομάδα / #lookingforteam, `στατιστικα` = #στατιστικά / #stats, ...); an English
   alias typed by a user or used in `/feed?tag=` resolves to the same key. Trending groups by key, so counts are identical in both languages.
3. Club English name = city exonym + translated nickname from the same word lists ("Larissa Halcyons", "Patras Bees", "Piraeus Echo").
   The jersey colour keeps hashing the Greek name (same colours in both languages). The OG card text is unchanged.
4. City Latin names become English exonyms (Athens, Piraeus, Patras, Heraklion, Larissa, Rhodes, ...); the rule parser accepts them.
5. One date helper `DisplayDate` (EN "Nov 22, 2026", "Sunday, Nov 1", "Nov 11"; EL output unchanged) and one ordinal helper
   (EN "82nd percentile"; EL unchanged "82ο εκατοστημόριο").

## Out of scope
OG card text, Greek copy changes, translating runtime user content.
