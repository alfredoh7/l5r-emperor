using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #699, THE "RINGS" CLUSTER: how a Ring ENTERS PLAY, recorded and listened to.
//
//   1. PROVENANCE. Every Ring that enters play gets a RingEntryRecord (MatchState.WlRings.Entries, keyed by the Ring's card id): whether it entered by its OWN printed text ("own-text") or by
//      another card's effect ("other-card"), the card/ability responsible and the turn. The record is overwritten every time the Ring enters play again, so it always answers "how did this Ring LAST
//      enter play". The core rules' Enlightenment Victory and the printed cards Fudo's Assurances and Unsettling Doubts read it (RingLastEnteredByOwnText); the older per-Ring mark
//      (AbilityUses[NoEnlightenmentKey]) is kept in step with it so nothing that still reads the mark changes. A Ring with no record counts as having entered by its own text (a Ring placed in play
//      directly, as test fixtures and old saves do).
//   2. THE TRIGGER. ReactionTriggers.RingEntersPlay: "After your Ring enters play". Every entry path records a "ring-entered" effect on the resolving action (RingEnteredKind); a Ring that enters
//      outside an action (an "After this card enters play" search, Asako Chukage's and Celestial Sword of the Dragon's) opens the same after-action window through a catalogued pseudo-action.
//      The window is the existing after-action-resolved Reaction window: optional, passable, the controller's own Reactions only.
//
// READINGS (recorded in docs/decisions/2026-10-06-699-rings.md; none is a confirmed ruling beyond Mendon's brief):
//   * Own text = the Ring's own recruit/put-into-play ability (Ring of X's recruit, a False Ring's "put this Ring into play"). Asako Usohara's "This counts as playing it by its own text" is own text.
//     Togashi's Guidance, Starting on the Path, Chukage's and Celestial Sword of the Dragon's searches put a Ring into play by ANOTHER card's text.
//   * Fudo's Assurances: "destroy a Province" names no owner, so the acting player chooses any Province that is not already destroyed.
// ======================================================================================================================================================================================

/// <summary>How a Ring LAST entered play (MatchState.WlRings.Entries): <see cref="WlRingsState.OwnText"/> or <see cref="WlRingsState.OtherCard"/>; the card and ability responsible; the turn.</summary>
public sealed record RingEntryRecord(string Mode, string? SourceCardId, string? AbilityId, int Turn);

/// <summary>What the Rings cluster keeps between commands.</summary>
public sealed class WlRingsState
{
    public const string OwnText = "own-text", OtherCard = "other-card";

    /// <summary>Ring card id to how it last entered play.</summary>
    public Dictionary<string, RingEntryRecord> Entries { get; set; } = [];

    /// <summary>A Ring a search put into play outside an action, waiting for FinishSearch to open its "after your Ring enters play" window.</summary>
    public string? SearchEnteredRingId { get; set; }
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(21400)]
    public WlRingsState WlRings { get; set; } = new();
}

public static class WlRingsAbilities
{
    public const string WitnessLook = "wl-witness-the-untold-look", FudoStraighten = "wl-fudo-assurances-straighten", FudoDestroy = "wl-fudo-assurances-destroy", EntrySearch = "wl-ring-entry-search";

    /// <summary>Witness the Untold: "Look at the top three cards of your Dynasty or Fate deck. Put them back in any order." (Control Your Destiny's two own decks, nothing but a reorder.)</summary>
    public static LookSpec WitnessLookSpec { get; } = new("wl-witness-the-untold", "Look at the top three cards of your Dynasty or Fate deck", [new("Fate"), new("Dynasty")], 3, [], Reorder: true);

    public static Dictionary<string, AbilityDefinition> AddWlRings(this Dictionary<string, AbilityDefinition> all)
    {
        // Witness the Untold (oracle-l5r-10357, Torn Asunder): "Reaction: After your Ring enters play, bow this card: Look at the top three cards of your Dynasty or Fate deck. Put them back in any order."
        all[WitnessLook] = new("Reaction", "After your Ring enters play, bow this card: Look at the top three cards of your Dynasty or Fate deck. Put them back in any order", "none",
            Trigger: ReactionTriggers.RingEntersPlay, Look: WitnessLookSpec, Costs: ActionCost.BowSource);
        // Fudo's Assurances (oracle-l5r-10416, Coils of Madness): "Battle/Open: Straighten a Ring." and "Open: If you control five Fudo Rings that last entered play by their own text, destroy a Province."
        all[FudoStraighten] = new("BattleOrOpen", "Battle/Open: Straighten a Ring", "ring-in-play-or-own-discard", UsageKey: "wl-fudo-assurances-straighten");
        all[FudoDestroy] = new("Open", "Open: If you control five Fudo Rings that last entered play by their own text, destroy a Province", "any-province", UsageKey: "wl-fudo-assurances-destroy");
        // Never on a card: the facts the after-action window reads when a Ring entered play outside an action (an entry search). Timing "Reaction" so no action trait mistakes it for one a player took.
        all[EntrySearch] = new("Reaction", "A Ring was put into play by a search an entering card's text made", "none");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private WlRingsState WlR => _state.WlRings;

    // ---- provenance ------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The ONE place a Ring enters play: records how (own text, or another card's effect), keeps the older Enlightenment mark in step, notes the turn for Brotherhood Schism, and puts the entry on
    /// the resolving action's record so "after your Ring enters play" is heard. The caller has already moved the card into play.</summary>
    private void RingEntersPlay(PlayerState player, CardInstance ring, bool byOwnText, CardInstance? source = null, string? abilityId = null)
    {
        WlR.Entries[ring.Id] = new RingEntryRecord(byOwnText ? WlRingsState.OwnText : WlRingsState.OtherCard, source?.Id, abilityId, _state.TurnNumber);
        if (byOwnText) ring.AbilityUses.Remove(NoEnlightenmentKey); else ring.AbilityUses[NoEnlightenmentKey] = 1;
        player.RingPutIntoPlayTurn = _state.TurnNumber;
        if (_runningCommand is not null) RecordActionEffect(ring.Id, RingEnteredKind);
    }

    /// <summary>Brotherhood Schism / Losing the Way: "Rings will not enter play if their owner has already put a Ring into play this turn" (every entry path asks this before it puts a Ring into play).</summary>
    private bool RingMayEnterPlay(PlayerState player) => _state.RingSchismTurn != _state.TurnNumber || player.RingPutIntoPlayTurn != _state.TurnNumber;

    /// <summary>"Last entered play by its own text": true unless the Ring's latest entry was another card's effect (no record at all counts as its own text).</summary>
    private bool RingLastEnteredByOwnText(CardInstance ring) =>
        !ring.AbilityUses.ContainsKey(NoEnlightenmentKey) && !(WlR.Entries.TryGetValue(ring.Id, out var entry) && entry.Mode != WlRingsState.OwnText);

    /// <summary>The Fudo Rings the player controls in play that last entered play by their own text.</summary>
    private int FudoRingsByOwnText(int playerId) =>
        _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Ring && Keyword(c, "Fudo") && RingLastEnteredByOwnText(c));

    // ---- the trigger -----------------------------------------------------------------------------------------------------------------------------------------------------

    // A Ring of the player's entered play with the action that just resolved (by its own recruit text, which every recruit ability id ends in, or on the action's record).
    private bool RingEnteredPlayFor(int playerId, ResolvedActionFacts facts) => YungRingEnteredPlay(facts, playerId);

    // AfterActionResolvedApplies' hook: the Reactions that name the trigger apply exactly when one of the holder's Rings entered play. null: not one of this cluster's.
    private bool? WlRingsAfterActionApplies(string abilityId, int playerId, ResolvedActionFacts facts) =>
        AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.Trigger == ReactionTriggers.RingEntersPlay ? RingEnteredPlayFor(playerId, facts) : null;

    // FinishSearch's hook (an entry-origin search ended): a Ring the search put into play opens the after-action window for it. True when a window opened.
    private bool WlRingEntrySearchFinished(PendingSearch search)
    {
        if (WlR.SearchEnteredRingId is not { } ringId) return false;
        WlR.SearchEnteredRingId = null;
        if (search.Origin != EntrySearchOrigin || _state.WinnerId is not null || !_state.Cards.ContainsKey(ringId)) return false;
        RecordActionEffect(ringId, RingEnteredKind);
        return FinishActionEffectTracking(WlRingsAbilities.EntrySearch, search.PlayerId, search.SourceCardId, search.ResumeTo ?? search.PlayerId);
    }

    // ---- validation and effects ------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlRings(ValidationHookContext context)
    {
        var (id, command, player) = (context.id, context.command, context.player);
        switch (id)
        {
            // "Straighten a Ring": any Ring in play (the listing category also names the player's own Rings in the Fate discard pile).
            case WlRingsAbilities.FudoStraighten when command.TargetId is { } ringId && _state.Cards.TryGetValue(ringId, out var ring):
                Require(Definition(ring).Type == CardType.Ring && InPlay(ring), "Target a Ring in play.");
                break;
            case WlRingsAbilities.FudoDestroy:
                Require(FudoRingsByOwnText(player.Id) >= 5, "You must control five Fudo Rings that last entered play by their own text.");
                break;
        }
    }

    private bool? RunWlRings(RunHookContext context)
    {
        var (id, ability, command, player, source, target) = (context.id, context.ability, context.command, context.player, context.source, context.target);
        switch (id)
        {
            case WlRingsAbilities.WitnessLook:
                SuspendOpenWindow(); // a Reaction's look sets the window it was played in aside (ReactionsAreActions.cs)
                BeginLook(ability.Look!, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case WlRingsAbilities.FudoStraighten:
                Straighten(target!);
                return false;
            case WlRingsAbilities.FudoDestroy:
                {
                    // The conditions are checked again as the effect resolves: five Fudo Rings may have been lost to a Reaction.
                    if (FudoRingsByOwnText(player.Id) < 5) return false;
                    var owner = command.TargetPlayerId!.Value;
                    var province = Player(owner).Provinces[command.Province!.Value];
                    if (province.Destroyed) return false;
                    Emit("provinceTargeted", $"{Definition(source!).Name}: {player.Name} destroys {Player(owner).Name}'s province {province.Index + 1}.", player.Id, source!.Id);
                    DestroyProvince(owner, province);
                    return false;
                }
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlRingsHooks()
    {
        EngineHooks.Abilities.Register("AddWlRings", 15400, static abilities => abilities.AddWlRings());
        EngineHooks.Looks.Register("WlRingsLooks", 15400, static () => [WlRingsAbilities.WitnessLookSpec]);
        EngineHooks.ValidateLate.Register("ValidateWlRings", 15400, static (engine, context) => engine.ValidateWlRings(context));
        EngineHooks.RunEffects.Register("RunWlRings", 15400, static (engine, context) => engine.RunWlRings(context));
    }
}
