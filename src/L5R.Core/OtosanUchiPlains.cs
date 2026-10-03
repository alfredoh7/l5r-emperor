namespace L5R.Core;

// Plains of Otosan Uchi (oracle-l5r-5979, Emperor Edition Region, issue #554). Printed text: "This Province holds two
// Dynasty cards; do not reveal the second card the same turn this Region enters play. Before this Province is
// destroyed, lose 10 Honor." The catalogue's own MRP note: "If this card is destroyed, the extra Dynasty card no
// longer automatically goes away (though the next time the province refills, it only holds one card like normal).
// This card no longer reveals one of its two Dynasty cards the turn it comes into play."
//
// Mendon's ruling (2026-09-30, issue #554):
//   * the 10-Honor loss fires whenever the province is destroyed; exact ordering against the destruction itself is
//     immaterial, so it is applied first thing in DestroyProvince, before anything else happens there;
//   * provinces reveal left to right (ContinueEventsReveal's own province-index order, unchanged); reaching this
//     province with BOTH slots still face down opens "provinceOrder" -- Left/Right, "Which card in <Region> would
//     you like to reveal first?" -- then each slot reveals+resolves in that order, exactly like the ordinary
//     single-slot reveal already does per card, one at a time;
//   * the second slot is created (empty, then refilled face down, never revealed) the instant the Region attaches --
//     always after that Events Phase's own reveal loop has already passed this province's index, so nothing needs
//     revealing THIS turn regardless of how attachment happened (matches the MRP note in different words);
//   * both cards share the underlying province's one battlefield and its adjacency: both simply carry
//     ProvinceIndex == province.Index, exactly like the primary slot already does, so BattlefieldProvinceIndex and
//     AdjacentProvinces (both index-only) need no change at all;
//   * "target a card in a province" effects (Shosuro Toson's Reaction is Mendon's own example) may hit only one of
//     the two: DiscardCurrentProvinceCard (EffectChoice.cs) and its own identity-aware callers open "provinceCardChoice"
//     (Left/Right, or the face-up card's own title) whenever a province holds two candidates.
//
// Scope note: FindProvinceSlot below covers the two common card->play paths named in the #554 blocker note (Purchase,
// RecruitReserve). The ~14 far narrower card-specific "the province whose CardId is this card" lookups elsewhere in
// the codebase (Gempukku Pass, several Emperor Edition remaining-cards effects, ...) are untouched: on the rare
// combo where one of THOSE names a card that happens to sit in Otosan Uchi's second slot, they throw (Single() finds
// no match) rather than silently doing the wrong thing. A documented limitation, not a silent one.
public static class OtosanUchiPlains
{
    public const string Effect = "otosan-uchi-plains";
}

/// <summary>A province-card-targeting effect (Shosuro Toson's Reaction, Mendon's own example) paused because its
/// province holds two candidates. AbilityId tags which continuation ResolveProvinceCardChoice runs once answered;
/// SourceCardId is that ability's own source, kept for the continuation (OtosanUchiPlains.cs).</summary>
public sealed class PendingProvinceCardChoice
{
    public int ProvinceOwnerId { get; set; }
    public int ProvinceIndex { get; set; }
    public int PlayerId { get; set; }
    public string AbilityId { get; set; } = "";
    public string? SourceCardId { get; set; }
}

public sealed partial class MatchEngine
{
    private const int OtosanSlotPrimary = 0, OtosanSlotSecond = 1;

    // Whether NEW cards may refill into this province's second slot: only while Otosan Uchi's own Region is still
    // attached. A card ALREADY sitting in CardId2 is untouched by this check -- see the MRP note above.
    private bool GrantsSecondDynastySlot(ProvinceState province) =>
        province.RegionId is string regionId && Definition(Card(regionId)).Effect == OtosanUchiPlains.Effect;

    private static void SwapProvinceSlots(ProvinceState province) => (province.CardId, province.CardId2) = (province.CardId2, province.CardId);

    // The province and slot (0 = CardId, 1 = CardId2) a given card currently sits in. See the scope note above:
    // Purchase and RecruitReserve use this; most other card-specific province lookups do not.
    private (ProvinceState Province, int Slot) FindProvinceSlot(PlayerState player, string cardId)
    {
        foreach (var province in player.Provinces)
        {
            if (province.CardId == cardId) return (province, OtosanSlotPrimary);
            if (province.CardId2 == cardId) return (province, OtosanSlotSecond);
        }
        throw new InvalidOperationException($"No province holds card '{cardId}'.");
    }
    private static void ClearProvinceSlot(ProvinceState province, int slot) { if (slot == OtosanSlotPrimary) province.CardId = null; else province.CardId2 = null; }
    private void RefillProvinceSlot(PlayerState player, ProvinceState province, int slot) { if (slot == OtosanSlotPrimary) Refill(player, province); else RefillSecondSlot(player, province); }

    // ---- province-card targeting (Shosuro Toson's Reaction, Mendon's own #554 example) --------------------------------

    // The Dynasty card(s) currently sitting in this province, in slot order -- 0, 1 or 2 candidates.
    private List<CardInstance> ProvinceCardCandidates(ProvinceState province)
    {
        var result = new List<CardInstance>();
        if (province.CardId is string id) result.Add(Card(id));
        if (province.CardId2 is string id2) result.Add(Card(id2));
        return result;
    }

    // Discards whichever slot this specific card sits in (not necessarily CardId) and refills that same slot.
    private void DiscardProvinceCandidate(ProvinceState province, CardInstance card)
    {
        var owner = Player(_state.Players.First(p => p.Provinces.Contains(province)).Id);
        var slot = province.CardId == card.Id ? OtosanSlotPrimary : OtosanSlotSecond;
        ClearProvinceSlot(province, slot);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; card.FaceUp = true;
        Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {owner.Name}'s province.", owner.Id, card.Id);
        RefillProvinceSlot(owner, province, slot);
    }

    // Opens "provinceCardChoice" (Left/Right; the face-up card's own title replaces either label once face up --
    // MatchQueries.cs) for a province-targeting effect with two candidates. Always called only when there ARE two
    // (ProvinceCardCandidates().Count > 1); the caller's own execution case returns this result.
    private bool BeginProvinceCardChoice(ProvinceState province, int deciderId, string abilityId, string sourceCardId)
    {
        var owner = _state.Players.First(p => p.Provinces.Contains(province)).Id;
        // A Reaction's own window is still open around this (Toson's own engage Reaction, Mendon's example): suspend it
        // first, the same way every other paused-mid-Reaction decision does (TenbatsuEngage's own look, for one), so it
        // is given back once this resolves rather than silently overwritten.
        SuspendOpenWindow();
        _state.PendingProvinceCardChoice = new() { ProvinceOwnerId = owner, ProvinceIndex = province.Index, PlayerId = deciderId, AbilityId = abilityId, SourceCardId = sourceCardId };
        _state.PendingChoice = new("provinceCardChoice", deciderId, sourceCardId, "Choose which card in the province to affect.");
        Emit("provinceCardChoiceOpened", $"{Player(deciderId).Name} chooses which card in the province to affect.", deciderId, sourceCardId);
        return true;
    }

    private bool HandleRegisteredProvinceCardChoice(string type, GameCommand command)
    {
        if (type != "provinceCardChoice") return false;
        ResolveProvinceCardChoice(command);
        return true;
    }

    private void ResolveProvinceCardChoice(GameCommand command)
    {
        var pending = _state.PendingProvinceCardChoice!;
        Require(command.Type == "provinceCardChoice" && command.PlayerId == pending.PlayerId && command.Choice is "left" or "right",
            "Choose which card to affect: left or right.");
        var province = Player(pending.ProvinceOwnerId).Provinces[pending.ProvinceIndex];
        var chosen = command.Choice == "left" ? (province.CardId is string a ? Card(a) : null) : (province.CardId2 is string b ? Card(b) : null);
        var player = Player(command.PlayerId);
        _state.PendingChoice = null; _state.PendingProvinceCardChoice = null;
        if (chosen is not null) DiscardProvinceCandidate(province, chosen);
        // Only one continuation exists today (Toson's own): a future province-card-targeting effect built the same
        // way adds its own case here, keyed by pending.AbilityId, exactly like FocusEffectChoice's own dispatch.
        if (pending.AbilityId == EmperorP3Abilities.TosonEngage) FinishTosonAfterProvinceChoice(player, Card(pending.SourceCardId!), chosen);
    }

    // AttachRegionToProvince's own hook (like AfterRegionAttachedP6 beside it): the second slot is created the
    // instant Otosan Uchi attaches, always after this Events Phase's own reveal loop already passed this province
    // (whether attached by a fresh reveal or a deck search elsewhere) -- so it is simply refilled face down, nothing
    // needs to dodge revealing it "this turn" as a special case.
    private void AfterRegionAttachedOtosanUchi(ProvinceState province, CardInstance region)
    {
        if (Definition(region).Effect != OtosanUchiPlains.Effect) return;
        RefillSecondSlot(Player(region.OwnerId), province);
    }

    // Refill's own mirror for the second slot (issue #554): same shape, gated on GrantsSecondDynastySlot rather than
    // HoldsNoDynastyCards.
    private void RefillSecondSlot(PlayerState player, ProvinceState province)
    {
        if (province.Destroyed || province.CardId2 is not null || player.DynastyDeck.Count == 0 || !GrantsSecondDynastySlot(province)) return;
        var id = player.DynastyDeck[0]; player.DynastyDeck.RemoveAt(0);
        province.CardId2 = id; var card = Card(id); card.Zone = Zone.Province; card.ProvinceIndex = province.Index; card.FaceUp = false;
        Emit("provinceRefilled", $"{player.Name} refilled the second card of province {province.Index + 1} face down.", player.Id);
    }

    // ---- Events Phase reveal --------------------------------------------------------------------------------------

    // ContinueEventsReveal's own branch for any province currently holding (or mid-sequence for) a second Dynasty
    // slot -- routed there instead of the ordinary single-slot block below it. Returns true when it stopped for a
    // decision (the caller returns, same convention as every other deferred reveal); false once fully settled, at
    // which point EventsRevealNext has already been advanced past this province.
    private bool HandleOtosanUchiProvinceReveal(PlayerState player, ProvinceState province)
    {
        if (_state.OtosanUchiProvinceIndex != province.Index)
        {
            var firstDown = province.CardId is { } id1 && !Card(id1).FaceUp;
            var secondDown = province.CardId2 is { } id2 && !Card(id2).FaceUp;
            if (firstDown && secondDown) return BeginProvinceRevealOrder(player, province); // asks; ResolveProvinceOrder sets the queue and resumes here.
            _state.OtosanUchiProvinceIndex = province.Index;
            _state.OtosanUchiRevealQueue = firstDown ? [OtosanSlotPrimary] : secondDown ? [OtosanSlotSecond] : [];
        }
        return DrainOtosanUchiReveal(player, province);
    }

    private bool BeginProvinceRevealOrder(PlayerState player, ProvinceState province)
    {
        _state.PendingChoice = new("provinceOrder", player.Id, null,
            $"Which card in {(province.RegionId is string rid ? Definition(Card(rid)).Name : "the province")} would you like to reveal first?");
        _state.OtosanUchiProvinceIndex = province.Index;
        Emit("provinceOrderOpened", $"{player.Name} chooses which card in province {province.Index + 1} to reveal first.", player.Id);
        return true;
    }

    private bool HandleRegisteredProvinceOrder(string type, GameCommand command)
    {
        if (type != "provinceOrder") return false;
        ResolveProvinceOrder(command);
        return true;
    }

    private void ResolveProvinceOrder(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "provinceOrder" && command.PlayerId == pending.PlayerId && command.Choice is "left" or "right",
            "Choose which card to reveal first: left or right.");
        var player = Player(command.PlayerId);
        _state.PendingChoice = null;
        _state.OtosanUchiRevealQueue = command.Choice == "left" ? [OtosanSlotPrimary, OtosanSlotSecond] : [OtosanSlotSecond, OtosanSlotPrimary];
        Emit("provinceOrderChosen", $"{player.Name} reveals the {command.Choice} card first.", player.Id);
        ContinueEventsReveal(player);
    }

    // Drains OtosanUchiRevealQueue for this province, one slot at a time, reusing the ordinary single-slot
    // ResolveEvent/ResolveRegion/ResolveCelestial/DiscardRevealedHoldingOnTestedLand machinery UNCHANGED for slot 2
    // by temporarily swapping it into CardId (SwapProvinceSlots) for the one call that needs it, then swapping back.
    // A deferred sub-decision (Harsh Choices, a search, ...) leaves OtosanUchiSwapped true across the "return true";
    // the next call restores it FIRST, before anything else, regardless of what that sub-decision did (discarded and
    // refilled the slot, or destroyed the whole province -- DestroyProvince clears both slots either way).
    private bool DrainOtosanUchiReveal(PlayerState player, ProvinceState province)
    {
        if (_state.OtosanUchiSwapped) { SwapProvinceSlots(province); _state.OtosanUchiSwapped = false; }
        while (_state.OtosanUchiRevealQueue.Count > 0)
        {
            var slot = _state.OtosanUchiRevealQueue[0];
            _state.OtosanUchiRevealQueue.RemoveAt(0);
            var id = slot == OtosanSlotPrimary ? province.CardId : province.CardId2;
            if (id is not string cardId || Card(cardId).FaceUp) continue; // nothing to reveal for this slot (empty, or already face up some other way)
            if (slot == OtosanSlotSecond) SwapProvinceSlots(province);
            var card = Card(cardId);
            card.FaceUp = true;
            Emit("provinceRevealed", $"{player.Name} revealed {Definition(card).Name} in province {province.Index + 1}.", player.Id, card.Id);
            var deferred = false;
            if (Definition(card).Type == CardType.Event) deferred = ResolveEvent(player, province, card);
            else if (Definition(card).Type == CardType.Region) deferred = ResolveRegion(player, province, card);
            else if (Definition(card).Type == CardType.Celestial) ResolveCelestial(player, province, card);
            else if (Definition(card).Type == CardType.Holding) DiscardRevealedHoldingOnTestedLand(player, province, card);
            if (slot == OtosanSlotSecond) { if (deferred) _state.OtosanUchiSwapped = true; else SwapProvinceSlots(province); }
            if (deferred) return true;
        }
        _state.OtosanUchiProvinceIndex = null;
        _state.EventsRevealNext = province.Index + 1;
        return false;
    }

    // ---- province destruction --------------------------------------------------------------------------------------

    // DestroyProvince's own hook: "Before this Province is destroyed, lose 10 Honor" and both slots' cards go the
    // way the primary slot already does. Called at the very top, before province.Destroyed is set -- Mendon's
    // ruling treats the exact before/after ordering as immaterial, so this is simply the simplest place to put it.
    private void BeforeDestroyOtosanUchiProvince(int owner, ProvinceState province)
    {
        if (province.RegionId is not string regionId || Definition(Card(regionId)).Effect != OtosanUchiPlains.Effect) return;
        var region = Card(regionId);
        GainHonor(owner, -10, Definition(region).Name, region);
        if (province.CardId2 is string id2) { var card = Card(id2); card.Zone = Zone.DynastyDiscard; card.FaceUp = true; card.ProvinceIndex = null; }
        province.CardId2 = null;
    }

    // Test-only hooks (see e.g. ProvinceStrengthForTest, EmperorRemainingE.cs): DestroyProvince and DestroyRegion are
    // private, reached in real play only through a full battle or Brawl respectively.
    internal void DestroyProvinceForTest(int owner, int index) => DestroyProvince(owner, Player(owner).Provinces[index]);
    internal void DestroyRegionForTest(string regionId) => DestroyRegion(Card(regionId));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before catalogue construction.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterOtosanUchiPlainsHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredProvinceOrder", 3550, static (engine, type, command) => engine.HandleRegisteredProvinceOrder(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredProvinceCardChoice", 3560, static (engine, type, command) => engine.HandleRegisteredProvinceCardChoice(type, command));
    }
}
