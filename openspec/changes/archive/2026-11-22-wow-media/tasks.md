# Tasks — wow-media

- [x] 1. Stubs (empty catalog, JerseyStyle returning defaults, empty seeder) + `MediaEntities` + migration `WowMedia` + failing `MediaTests` (23). **Red run recorded.**
- [x] 2. Assets: 30 CC0 photos (Wikimedia Commons) + 6 generated graphics videos, compressed, logged in `docs/ASSETS.md`, each inspected.
- [x] 3. MediaSeeder, MediaService, JerseyStyle, MediaResource (el/en), components (JerseyAvatar, MediaTile, PostMedia, StoriesBar, ProfileHighlights), `/stories/{slug}`, bp-media.css, bp-media.js, seams.
- [x] 4. Green: `BALLPASS_TESTDB_SUFFIX=_media dotnet test`. **Green run recorded.**
- [x] 5. Screenshots 1280 + 390 (feed, carousel, video, lightbox, stories bar, profile highlights, story page, search, coach jersey) read and fixed.

## Red run (task 1)
`BALLPASS_TESTDB_SUFFIX=_media dotnet test --filter FullyQualifiedName~MediaTests` (assertion failures on empty catalog / missing files / missing markup)
```
Failed!  - Failed:    23, Passed:     0, Skipped:     0, Total:    23
```

## Green run (task 4)
```
Passed!  - Failed: 0, Passed:   454, Skipped: 0, Total:   454   (431 existing + 23 new)
```

## Fixed after reading screenshots
- Video label overlapped the graphic's own title: label moved to a bar under videos.
- Photo tiles stretched to the video tile's height in the reel grid: `align-items: start`.
- Mobile video letterboxed at 4:3: videos keep 16:9, only photos use 4:3 on phones.
- Full-page screenshots show lazy images / carousel dots wrongly (capture artefact); viewport screenshots after scrolling are the proof.
