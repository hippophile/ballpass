# wow-media (Phase D package "Media"; serves feed, M1 athlete profile)

## Driver
Founder feedback 2026-11-22: "a feed with photos, images, videos ... this is too stiff". Photos + short videos in posts and
profiles, a stories bar, and illustrated jersey avatars instead of initials-only circles.

## Decision
1. Self-hosted assets under `wwwroot/media/` (photos WebP <=1280px, videos H.264 MP4 <=720p with WebP poster). Total <= 40 MB.
   Photos: CC0 / Unsplash-licence stock only, no recognisable faces, every file logged in `docs/ASSETS.md`.
   Videos: generated motion graphics (ffmpeg) labelled "Γραφικό · demo" - never presented as real footage.
2. `MediaItem` entity (0..4 per post, 1..3 per athlete reel), migration `WowMedia`, deterministic `MediaSeeder`
   hooked from `DemoDataSeeder` with one call.
3. `PostMedia` (image / grid-carousel with scroll-snap / video with poster), CSS-only `:target` lightbox, `bp-media.js`
   (play >= 60 % visible, pause otherwise, never under prefers-reduced-motion).
4. `StoriesBar` on /feed (athletes with a reel), `ProfileHighlights` (`#highlights`) replacing "no video yet",
   `/stories/{slug}` overlay page with prev/next.
5. `JerseyAvatar` (inline SVG jersey, club colour, number 0-99 from id, initials as text; coach variant with clipboard badge).

## Caveats
- Stock photos never show the named athlete: every item carries a visible chip "Εικόνα αρχείου · demo" or "Γραφικό · demo".
- openspec/config.yaml lists "uploads of real photos/video files" out of scope: nothing here is uploaded by users.
- Media is read per post (indexed query, <= 10 per feed page), not joined into the feed query (feed package owns it).
