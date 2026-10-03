namespace L5R.Core;

// Embers of War pass (issue #460), batch 5: primitives shared by more than one card of the batch, or too small to deserve their own file.
// Each is documented at its own use in CardAbilities.cs/MatchEngine.cs; this file holds only the execution/validation helpers not already
// folded into an existing shared file (HonorAndTreacheryPass.cs's StaticGrantedAbilityIds gained The Ki-Rin's Path's own entry directly;
// FocusEffectChoice.cs/MatchEngine.cs gained The Swell of Strength's Focus Effect and static Force bonus directly, matching how the shapes
// they extend already live in those files).
public sealed partial class MatchEngine
{
    // Incite the Phantoms (oracle-l5r-3732): "While this Shugenja is Earth, your Spirits contribute Force to their army even while bowed."
    // Read live by EliteEffect (MatchEngine.cs), the same way every other conditional Force-while-bowed grant already is -- true while the
    // controller has an in-play copy of the Spell (Zone.Attached counts as in play, InPlay()) whose own wielder currently has the Earth
    // keyword. Scoped to every Spirit the controller owns (both Personality and Follower Spirits -- EliteEffect is read by both contribution
    // helpers), not only the Spell's own wielder, which is why this is its own check rather than reusing AttachGrantsEliteEffectWhileWielderHas
    // (that field is scoped to a single card, the wielder itself).
    private const string InciteThePhantomsForceEffect = "incite-the-phantoms-force";

    private bool InciteThePhantomsActive(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Spell && Definition(c).Effect == InciteThePhantomsForceEffect
            && c.AttachedTo is string wielderId && Keyword(Card(wielderId), "Earth"));
}
