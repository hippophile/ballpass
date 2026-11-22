using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Media;

/// <summary>One self-hosted file (relative to /media/). Theme picks which posts it may illustrate. Generated from the asset build; every file is logged in docs/ASSETS.md.</summary>
public record MediaAsset(string Id, MediaKind Kind, string File, string? Poster, int Width, int Height, string AltEl, string AltEn, bool Graphic, string Theme);

public static class MediaCatalog
{
    private static MediaAsset New(string id, MediaKind kind, string file, string? poster, int w, int h, string el, string en, bool graphic, string theme) =>
        new(id, kind, file, poster, w, h, el, en, graphic, theme);

    public static readonly IReadOnlyList<MediaAsset> All =
    [
        New("dribble-teal-court", MediaKind.Photo, "photos/dribble-teal-court.webp", null, 1280, 720, "Ντρίμπλα σε υπαίθριο γήπεδο, κάδρο από τη μέση και κάτω", "Dribbling on an outdoor court, framed from the waist down", false, "action"),
        New("ball-above-hoop-trees", MediaKind.Photo, "photos/ball-above-hoop-trees.webp", null, 1280, 853, "Μπάλα πάνω από στεφάνι με δέντρα στο βάθος", "Ball above a hoop with trees behind", false, "net"),
        New("hooded-figure-fog", MediaKind.Photo, "photos/hooded-figure-fog.webp", null, 1280, 853, "Φιγούρα με κουκούλα, από πίσω, μπροστά σε καλάθι στην ομίχλη", "Hooded figure seen from behind in front of a hoop in the fog", false, "action"),
        New("hoop-from-below-bw", MediaKind.Photo, "photos/hoop-from-below-bw.webp", null, 1280, 821, "Ασπρόμαυρο ταμπλό και στεφάνι από κάτω", "Black and white backboard and hoop from below", false, "net"),
        New("hoop-white-sky", MediaKind.Photo, "photos/hoop-white-sky.webp", null, 1280, 853, "Στεφάνι και κολώνα σε λευκό ουρανό", "Hoop and pole against a white sky", false, "net"),
        New("pink-wall-court", MediaKind.Photo, "photos/pink-wall-court.webp", null, 1280, 853, "Ροζ-μωβ γήπεδο μπροστά από τοίχο με ταμπλό", "Pink and purple court in front of a wall with a backboard", false, "court"),
        New("ball-over-court", MediaKind.Photo, "photos/ball-over-court.webp", null, 1280, 853, "Μπάλα στον αέρα πάνω από γήπεδο, χέρι από κάτω", "Ball in the air above a court, a hand below", false, "ball"),
        New("dark-gym-wide", MediaKind.Photo, "photos/dark-gym-wide.webp", null, 1280, 848, "Σκοτεινό κλειστό γυμναστήριο, μακρινή φιγούρα", "Dark indoor gym with a distant figure", false, "gym"),
        New("hoop-blue-sky", MediaKind.Photo, "photos/hoop-blue-sky.webp", null, 1280, 851, "Στεφάνι με δίχτυ σε μπλε ουρανό", "Hoop with a net against a blue sky", false, "net"),
        New("net-from-below", MediaKind.Photo, "photos/net-from-below.webp", null, 1280, 857, "Δίχτυ και στεφάνι από κάτω", "Net and hoop from below", false, "net"),
        New("sneakers-in-gym", MediaKind.Photo, "photos/sneakers-in-gym.webp", null, 1280, 787, "Αθλητικά παπούτσια σε γυμναστήριο, από πίσω", "Sneakers in a gym, seen from behind", false, "shoes"),
        New("shooter-from-behind", MediaKind.Photo, "photos/shooter-from-behind.webp", null, 854, 1280, "Παίκτης από πίσω σημαδεύει καλάθι", "Player seen from behind aiming at the hoop", false, "action"),
        New("rusty-hoop-fence", MediaKind.Photo, "photos/rusty-hoop-fence.webp", null, 854, 1280, "Σκουριασμένο καλάθι σε παλιό γήπεδο με συρμάτινο φράχτη", "Rusty hoop on an old court with a wire fence", false, "court"),
        New("court-tower-block", MediaKind.Photo, "photos/court-tower-block.webp", null, 854, 1280, "Γήπεδο μπροστά από πολυκατοικία, μακρινές φιγούρες", "Court in front of an apartment block, distant figures", false, "court"),
        New("hoop-palm-silhouette", MediaKind.Photo, "photos/hoop-palm-silhouette.webp", null, 1280, 854, "Καλάθι σε σιλουέτα με φοίνικες στο ηλιοβασίλεμα", "Hoop silhouetted against palm trees at sunset", false, "net"),
        New("beach-court-sunset", MediaKind.Photo, "photos/beach-court-sunset.webp", null, 1280, 562, "Υπαίθριο γήπεδο στο ηλιοβασίλεμα, μακρινές φιγούρες", "Outdoor court at sunset with distant figures", false, "court"),
        New("palming-the-ball", MediaKind.Photo, "photos/palming-the-ball.webp", null, 1280, 853, "Χέρι κρατά μπάλα μπροστά από γήπεδο", "A hand holding a ball in front of a court", false, "ball"),
        New("aerial-court-players", MediaKind.Photo, "photos/aerial-court-players.webp", null, 1280, 720, "Γήπεδο από ψηλά με μικρούς παίκτες", "Aerial view of a court with small players", false, "court"),
        New("hoop-in-trees", MediaKind.Photo, "photos/hoop-in-trees.webp", null, 1280, 854, "Παλιό ταμπλό και στεφάνι ανάμεσα σε δέντρα", "Old backboard and hoop among trees", false, "net"),
        New("shooter-orange-jacket", MediaKind.Photo, "photos/shooter-orange-jacket.webp", null, 1280, 853, "Παίκτης από πίσω ετοιμάζεται να σουτάρει", "Player seen from behind about to shoot", false, "action"),
        New("ball-on-court-line", MediaKind.Photo, "photos/ball-on-court-line.webp", null, 1280, 848, "Μπάλα δίπλα σε λευκή γραμμή γηπέδου", "Ball next to a white court line", false, "ball"),
        New("court-painted-key", MediaKind.Photo, "photos/court-painted-key.webp", null, 854, 1280, "Χρωματιστή ρακέτα γηπέδου, μακρινή φιγούρα", "Painted key of a court with a tiny distant figure", false, "court"),
        New("hoop-cream-sky", MediaKind.Photo, "photos/hoop-cream-sky.webp", null, 1280, 853, "Ταμπλό και στεφάνι σε κρεμ ουρανό", "Backboard and hoop against a cream sky", false, "net"),
        New("backboard-blue", MediaKind.Photo, "photos/backboard-blue.webp", null, 860, 1280, "Ταμπλό και στεφάνι σε καθαρό γαλάζιο ουρανό", "Backboard and hoop in a clear blue sky", false, "net"),
        New("court-long-shadows", MediaKind.Photo, "photos/court-long-shadows.webp", null, 960, 1280, "Υπαίθριο γήπεδο με μεγάλες σκιές", "Outdoor court with long shadows", false, "court"),
        New("park-court-grass", MediaKind.Photo, "photos/park-court-grass.webp", null, 1280, 960, "Γήπεδο μπάσκετ σε πάρκο με γρασίδι", "Basketball court in a park with grass", false, "court"),
        New("fenced-court-wide", MediaKind.Photo, "photos/fenced-court-wide.webp", null, 1280, 768, "Περιφραγμένο γήπεδο με δύο ταμπλό", "Fenced court with backboards", false, "court"),
        New("asphalt-court-arc", MediaKind.Photo, "photos/asphalt-court-arc.webp", null, 1280, 606, "Άδειο γήπεδο από άσφαλτο με λευκή γραμμή τριπόντου", "Empty asphalt court with a white three-point line", false, "court"),
        New("white-sneakers", MediaKind.Photo, "photos/white-sneakers.webp", null, 1280, 960, "Λευκά αθλητικά παπούτσια σε κουτί", "White sneakers on their box", false, "shoes"),
        New("vintage-canvas-sneakers", MediaKind.Photo, "photos/vintage-canvas-sneakers.webp", null, 1280, 853, "Παλιά καμβά αθλητικά παπούτσια μουσείου", "Vintage canvas basketball sneakers from a museum", false, "shoes"),
        New("gfx-court-ball-trail", MediaKind.Video, "video/gfx-court-ball-trail.mp4", "video/gfx-court-ball-trail-poster.webp", 960, 540, "Γραφικό: τροχιά μπάλας προς το καλάθι σε σχηματικό γήπεδο", "Graphic: ball trail towards the hoop on a schematic court", true, "graphic"),
        New("gfx-stat-counter", MediaKind.Video, "video/gfx-stat-counter.mp4", "video/gfx-stat-counter-poster.webp", 960, 540, "Γραφικό: μετρητές στατιστικών πόντων, ριμπάουντ και ασίστ", "Graphic: animated counters for points, rebounds and assists", true, "graphic"),
        New("gfx-shot-chart", MediaKind.Video, "video/gfx-shot-chart.mp4", "video/gfx-shot-chart-poster.webp", 960, 540, "Γραφικό: χάρτης σουτ που γεμίζει με εύστοχα και άστοχα", "Graphic: shot chart filling with makes and misses", true, "graphic"),
        New("gfx-drill-cones", MediaKind.Video, "video/gfx-drill-cones.mp4", "video/gfx-drill-cones-poster.webp", 960, 540, "Γραφικό: ντρίμπλα σλάλομ ανάμεσα σε κώνους", "Graphic: slalom dribble between cones", true, "graphic"),
        New("gfx-scoreboard", MediaKind.Video, "video/gfx-scoreboard.mp4", "video/gfx-scoreboard-poster.webp", 960, 540, "Γραφικό: πίνακας αγώνα με χρονόμετρο και σκορ", "Graphic: scoreboard with game clock and score", true, "graphic"),
        New("gfx-season-bars", MediaKind.Video, "video/gfx-season-bars.mp4", "video/gfx-season-bars-poster.webp", 960, 540, "Γραφικό: ράβδοι πόντων ανά περίοδο που μεγαλώνουν", "Graphic: bars of points per season growing", true, "graphic"),
    ];
}
