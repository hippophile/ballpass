using Ballpass.Web.Features.Media;

namespace Ballpass.Web.Features.Flow;

/// <summary>
/// Stock choices that depend on the person shown next to them (SPEC §8 O3, M2). Every coach photo in the pool shows the same man,
/// so a woman coach never gets one: she gets the pool's only face-free scene (hands on a tactic board, credits.json faces:false,
/// the one landscape image of the pool; HomeTests lock that it has no faces).
/// </summary>
public static class FlowMedia
{
    public static StockImage CoachNoFace => StockMedia.CoachPool.Single(i => i.Width > i.Height);

    /// <summary>Inbox coach scene: the pool by coach id for a man, the face-free scene for a woman.</summary>
    public static StockImage CoachScene(int coachId, bool female) => female ? CoachNoFace : StockMedia.Coach(coachId);

    /// <summary>/demo featured coach card.</summary>
    public static StockImage FeaturedCoach(bool female) => female ? CoachNoFace : StockMedia.PersonaCoach;
}
