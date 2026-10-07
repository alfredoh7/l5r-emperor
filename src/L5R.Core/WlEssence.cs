namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #699, READ THE ESSENCE (spell-wave-56, oracle-l5r-10352, Torn Asunder; blocked in #674 as HIDDEN). The scan: "Open: Bow this card and target one face-down card in a province, or one or
// two such cards if this Shugenja is Void: Turn the targets face-up."
//
// MENDON'S RULING (#699): "Probably easiest to, if the shugenja is void, run the province flip effect twice in succession. For the Void variant, if the first flip is negated, so is the second,
// and the second cannot be negated if the first was not. These may be any provinces (either player's). Province selection should be limited only to face down cards. The second flip is
// optional if it is available."
//
// BUILT AS: ONE action ("wl-read-the-essence-flip"), costs paid once (the Spell bows). Its target is the existing "any-province" command parameter (TargetPlayerId + Province, the shape Second City,
// Study the Wheel and Ring of Earth use, so every client already lists the provinces, highlights them on hover and picks one), limited to a province that holds a FACE-DOWN card. The first flip
// is the action's effect: the card is turned face-up (a revealed Celestial enters play, as in Petty Squabbles, core rules). THEN, when this Shugenja is Void and some other face-down province card
// exists (the province a Celestial was refilled into counts: it is read after the first flip), a public "effectChoice" opens for the acting player (the engine-listed choice of
// BeginListedEffectChoice, answered by "chooseEffect") with one answer per face-down card, named by its province only (never its identity), and "Do not turn a second card" (the optional second
// flip: the player may decline it). The second flip is a plain step of the same action, so:
//   * a reaction that negates the action negates the whole action: no flip happens and the second is never offered (the action's effects do not run, ActionNegation.cs);
//   * the action was not negated when the choice opens (its window has closed), so the second flip cannot be negated.
// A card in a province is the only card a province holds (ProvinceState.CardId), so "the same province twice" is impossible; the second flip is "any other province".
// "If this Shugenja is Void" is read as the action's effect resolves (his keyword then, granted ones included). A province without a card or with a face-up card is refused.
// ======================================================================================================================================================================================

public static class WlEssenceAbilities
{
    public const string Flip = "wl-read-the-essence-flip";
    /// <summary>The prefix of a second-flip answer's id and effect ("wl-essence-flip:{owner}:{province}") and the id of the decline answer.</summary>
    public const string SecondFlipPrefix = "wl-essence-flip:", Decline = "wl-essence-decline";

    public static Dictionary<string, AbilityDefinition> AddWlEssence(this Dictionary<string, AbilityDefinition> all)
    {
        // Read the Essence (oracle-l5r-10352, Torn Asunder, Void): "Open: Bow this card and target one face-down card in a province, or one or two such cards if this Shugenja is Void: Turn the targets
        // face-up." The bow is the cost (paid once); the (optional) second flip is the action's second step.
        all[Flip] = new("Open", "Open, bow this card: Target a face-down card in a province: Turn it face-up (a Void Shugenja may then turn a second face-down card face-up)", "any-province", Costs: ActionCost.BowSource);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------------

    // The named province must hold a face-down card (the "any-province" validation already refused a missing or destroyed one).
    private void ValidateWlEssence(string id, GameCommand command)
    {
        if (id != WlEssenceAbilities.Flip) return;
        Require(command.TargetPlayerId is int owner && command.Province is int index && WlFaceDownCard(owner, index) is not null, "Choose a province that holds a face-down card.");
    }

    private CardInstance? WlFaceDownCard(int owner, int index)
    {
        if (owner is not (0 or 1) || index < 0 || index >= Player(owner).Provinces.Count) return null;
        var province = Player(owner).Provinces[index];
        return !province.Destroyed && province.CardId is { } cardId && !Card(cardId).FaceUp ? Card(cardId) : null;
    }

    // ---- turning a province card face-up ---------------------------------------------------------------------------------------------------------------------------------------

    // The flip, as Petty Squabbles and Second City make it: the card is public from now on, and a revealed Celestial enters play (core rules).
    private void WlTurnProvinceCardFaceUp(int owner, int index, string sourceName, int actingPlayerId)
    {
        var holder = Player(owner); var province = holder.Provinces[index];
        var card = Card(province.CardId);
        card.FaceUp = true;
        Emit("provinceRevealed", $"{sourceName}: {Definition(card).Name} in {holder.Name}'s province {province.Index + 1} is turned face-up.", actingPlayerId, card.Id);
        if (Definition(card).Type == CardType.Celestial) ResolveCelestial(holder, province, card);
    }

    // ---- execution ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this card's. false: the effects are done. true: the optional second flip is waiting for the acting player.
    private bool? RunWlEssence(string id, GameCommand command, PlayerState player, CardInstance? source, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBeforeBattleAction,
        HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        if (id != WlEssenceAbilities.Flip) return null;
        var name = Definition(source!).Name;
        WlTurnProvinceCardFaceUp(command.TargetPlayerId!.Value, command.Province!.Value, name, player.Id);
        var wielder = WielderOf(source!);
        if (wielder is null || !Keyword(wielder, "Void")) return false;
        var options = new List<EffectOption>();
        foreach (var holder in _state.Players)
            foreach (var province in holder.Provinces.Where(p => WlFaceDownCard(holder.Id, p.Index) is not null))
            {
                var effect = $"{WlEssenceAbilities.SecondFlipPrefix}{holder.Id}:{province.Index}";
                options.Add(new(effect, $"Turn the card in {(holder.Id == player.Id ? "your" : holder.Name + "'s")} province {province.Index + 1} face-up", [effect]));
            }
        if (options.Count == 0) return false;
        options.Add(new(WlEssenceAbilities.Decline, "Do not turn a second card face-up", []));
        return BeginListedEffectChoice(command, options.ToArray(), $"{name}: you may turn a second face-down province card face-up (this Shugenja is Void), or decline.",
            consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
    }

    // ApplyBranchEffect's arm for the second flip's answer; false for any other effect. The card may have changed since the choice opened (nothing can in between): it is re-checked.
    private bool ApplyWlEssenceBranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(WlEssenceAbilities.SecondFlipPrefix, StringComparison.Ordinal)) return false;
        var parts = effect[WlEssenceAbilities.SecondFlipPrefix.Length..].Split(':');
        var owner = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture); var index = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        if (WlFaceDownCard(owner, index) is not null)
            WlTurnProvinceCardFaceUp(owner, index, command.CardId is { } sourceId ? Definition(Card(sourceId)).Name : "Read the Essence", command.PlayerId);
        return true;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlEssenceHooks()
    {
        EngineHooks.Abilities.Register("AddWlEssence", 15300, static abilities => abilities.AddWlEssence());
        EngineHooks.ValidateLate.Register("ValidateWlEssence", 15300, static (engine, context) => engine.ValidateWlEssence(context.id, context.command));
        EngineHooks.RunEffects.Register("RunWlEssence", 15300, static (engine, context) => engine.RunWlEssence(context.id, context.command, context.player, context.source, context.consumedGrant, context.playedFromDiscard,
            context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
