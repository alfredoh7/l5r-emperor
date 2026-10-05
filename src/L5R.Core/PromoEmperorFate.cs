using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR PASS (issue #653), the Strategies and Spells (strategy-wave-112, spell-wave-51). Every card was read against its Promotional-Emperor scan; the scan wins over the scraped text
// (the Emperor-era scans print "Battle/Open:" where Oracle prints another designator, and some print no keyword line the catalogue invented).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md and docs/rules/RULINGS.md; none conflicts with them):
//   * UNIQUE PERSONALITY TARGETS and the small Battle/Open effects (Legendary Rivalry, Tamori Potion, Bar Fight, Might of the Sun, Summon Fushicho) ride the existing target / cost / attack hooks.
//   * "BEFORE THIS PHASE ENDS, PUT THIS STRATEGY INTO YOUR HAND" (Elemental Adroitness): PromoEmperorState.PhaseReturns, applied where the phase family changes (SetPhase calls PromoPhaseEnds).
//   * "DRAW CARDS UNTIL YOU HAVE EXACTLY TWO" (A Glimpse of Fate): the optional discard is a hand slot (0..1), the draw counts the hand without the Strategy being played.
//
// READINGS (each is also recorded in the report; none is a confirmed ruling):
//   * Tamori Potion's "different element keyword in his unit" counts Air, Earth, Fire, Water and Void among the Personality and every card attached to him.
//   * A Glimpse of Fate's discard is an effect ("you may"), never a cost, and cannot discard the Strategy that is being played; with more than two cards left in hand it draws nothing.
//   * Might of the Sun's "bow this Shugenja unless he is Fire or Thunder" is two catalogue entries sharing one use: a Fire or Thunder Shugenja pays nothing, any other bows (void-tattoo-return's shape).
// ======================================================================================================================================================================================

/// <summary>What the Promotional-Emperor Strategies and Spells keep between commands.</summary>
public sealed class PromoEmperorFateState
{
    /// <summary>Elemental Adroitness: "Before this phase ends, put this Strategy into your hand" -- the card, its owner, and the phase family (as PhaseFamily numbers it) it was played in.</summary>
    public List<PromoPhaseReturn> PhaseReturns { get; set; } = [];
}

public sealed record PromoPhaseReturn(string CardId, int PlayerId, int Family);

public sealed partial class MatchState
{
    [JsonPropertyOrder(1580)]
    public PromoEmperorFateState PromoFate { get; set; } = new();
}

public static class PromoEmperorFateAbilities
{
    public const string LegendaryRivalry = "pe-legendary-rivalry-bow", TamoriPotion = "pe-tamori-potion-attack", BarFightAttack = "pe-bar-fight-attack",
        MightBow = "pe-might-of-the-sun-bow", MightFree = "pe-might-of-the-sun-free", Fushicho = "pe-summon-fushicho-search", Glimpse = "pe-glimpse-of-fate-draw",
        Adroitness = "pe-elemental-adroitness-straighten";

    /// <summary>Target categories: a Unique Personality (either player's); a Ring in play (the out-of-play "ring-in-play-or-own-discard" listing, narrowed to play by validation).</summary>
    public const string UniquePersonality = "pe-unique-personality";

    /// <summary>The Effect key of Might of the Sun's static "+4F".</summary>
    public const string MightEffect = "pe-might-of-the-sun";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorFate(this Dictionary<string, AbilityDefinition> all)
    {
        // Legendary Rivalry (oracle-l5r-9991): "Battle/Open: Target a Unique Personality: Bow him."
        all[LegendaryRivalry] = new("BattleOrOpen", "Target a Unique Personality: Bow him", UniquePersonality);
        // Tamori Potion (oracle-l5r-7788): "Battle: Bow your performing Personality: Ranged 4 Attack with +1 strength if he is an Alchemist and +1 strength for each different element keyword in his unit."
        all[TamoriPotion] = new("Battle", "Bow your performing Personality: Ranged 4 Attack with +1 strength if he is an Alchemist and +1 strength for each different element keyword in his unit", "attack",
            Performer: "own-unbowed-personality-at-battle", Costs: ActionCost.BowPerformer);
        // Bar Fight (oracle-l5r-696): "Battle: Choose your performing unbowed Personality: Melee Attack with strength equal to 4 plus the number of Sake tokens he has." (Its Focus Effect is A Simple Yari's.)
        all[BarFightAttack] = new("Battle", "Choose your performing unbowed Personality: Melee Attack with strength equal to 4 plus the number of Sake tokens he has", "attack", Performer: "own-unbowed-personality-at-battle");
        // Might of the Sun (oracle-l5r-5046): "Battle: Bow this Shugenja unless he is Fire or Thunder and target an enemy card without attachments: Destroy it." (Its "+4F" is its Effect.)
        all[MightBow] = new("Battle", "Bow this Shugenja and target an enemy card without attachments: Destroy it", "enemy-card-without-attachments", Costs: ActionCost.BowWielder, UsageKey: "pe-might-of-the-sun");
        all[MightFree] = new("Battle", "He is Fire or Thunder: Target an enemy card without attachments: Destroy it", "enemy-card-without-attachments", UsageKey: "pe-might-of-the-sun");
        // Summon Fushicho (oracle-l5r-10414): "Limited: Bow this Shugenja and destroy this card: Search your Fate deck for an Item, show it, and put it in your hand."
        all[Fushicho] = new("Limited", "Bow this Shugenja and destroy this card: Search your Fate deck for an Item, show it, and put it in your hand", "none",
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice)], Search: SearchCatalog.ImperialGift);
        // A Glimpse of Fate (oracle-l5r-10597): "Battle/Open: You may discard a card. Draw cards until you have exactly two cards in your hand." The optional discard is a hand slot.
        all[Glimpse] = new("BattleOrOpen", "You may discard a card. Draw cards until you have exactly two cards in your hand", "slots",
            Slots: [new("discard", "hand", "A card in your hand to discard (optional)", 0, 1)]);
        // Elemental Adroitness (oracle-l5r-10600): "Battle: Straighten a target Ring. Before this phase ends, put this Strategy into your hand."
        all[Adroitness] = new("Battle", "Straighten a target Ring. Before this phase ends, put this Strategy into your hand", "ring-in-play-or-own-discard");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private PromoEmperorFateState PromoFate => _state.PromoFate;
    private static readonly string[] PromoElements = ["Air", "Earth", "Fire", "Water", "Void"];

    // ---- target categories -----------------------------------------------------------------------------------------------------------------------------------------------

    private bool PromoFateTargetAllows(string category, CardInstance target) => category switch
    {
        PromoEmperorFateAbilities.UniquePersonality => Definition(target).Type == CardType.Personality && Keyword(target, "Unique"),
        _ => false
    };

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidatePromoFate(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // The wielder's keyword decides which of the two entries is the legal one.
            case PromoEmperorFateAbilities.MightBow or PromoEmperorFateAbilities.MightFree:
            {
                Require(source is { Zone: Zone.Attached, AttachedTo: { } hostId } && _state.Cards.TryGetValue(hostId, out _), "This Spell must be attached to a Shugenja.");
                var wielder = Card(source!.AttachedTo);
                var elemental = Keyword(wielder, "Fire") || Keyword(wielder, "Thunder");
                Require(id == PromoEmperorFateAbilities.MightFree ? elemental : !elemental, elemental ? "This Shugenja is Fire or Thunder, so he does not bow." : "This Shugenja is neither Fire nor Thunder: he bows.");
                break;
            }
            // "Target a Ring": only one in play (the listing category also names the player's own Rings in the Fate discard pile).
            case PromoEmperorFateAbilities.Adroitness when command.TargetId is { } ringId && _state.Cards.TryGetValue(ringId, out var ring):
                Require(Definition(ring).Type == CardType.Ring && InPlay(ring), "Target a Ring in play.");
                break;
            // The discard is the player's choice among the other cards in hand: never the Strategy being played.
            case PromoEmperorFateAbilities.Glimpse when ability.Slots is not null:
                Require(SlotCards(command, "discard").All(card => card.Id != command.CardId), "A Strategy being played cannot be discarded to itself.");
                break;
        }
    }

    // ---- attacks ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private int? PromoFateAttackStrength(string abilityId, string? performerId)
    {
        switch (abilityId)
        {
            case PromoEmperorFateAbilities.BarFightAttack:
                return 4 + (performerId is not null && _state.Cards.TryGetValue(performerId, out var fighter) ? TokenCount(fighter, "Sake") : 0);
            case PromoEmperorFateAbilities.TamoriPotion:
            {
                if (performerId is null || !_state.Cards.TryGetValue(performerId, out var chemist)) return 4;
                var elements = UnitMembers(chemist).SelectMany(member => PromoElements.Where(element => Keyword(member, element))).Distinct(StringComparer.Ordinal).Count();
                return 4 + (Keyword(chemist, "Alchemist") ? 1 : 0) + elements;
            }
            default:
                return null;
        }
    }

    private static string? PromoFateAttackKind(string abilityId) => abilityId switch
    {
        PromoEmperorFateAbilities.TamoriPotion => "Ranged",
        PromoEmperorFateAbilities.BarFightAttack => "Melee",
        _ => null
    };

    // ---- Force ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Might of the Sun: "This Shugenja has +4F." A static trait of the Spell while it is attached.
    private int PromoMightForce(CardInstance card) =>
        Definition(card).Type == CardType.Personality ? 4 * Attachments(card.Id).Count(a => Definition(a).Type == CardType.Spell && Definition(a).Effect == PromoEmperorFateAbilities.MightEffect) : 0;

    // ---- phase end ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Elemental Adroitness: "Before this phase ends, put this Strategy into your hand." Called by SetPhase just before the phase changes; the whole Attack-through-battle stretch is one phase.
    private void PromoPhaseEnds(Phase next)
    {
        if (PromoFate.PhaseReturns.Count == 0 || PhaseFamily(next) == PhaseFamily(_state.Phase)) return;
        foreach (var due in PromoFate.PhaseReturns.Where(r => r.Family == PhaseFamily(_state.Phase)).ToArray())
        {
            PromoFate.PhaseReturns.Remove(due);
            if (!_state.Cards.TryGetValue(due.CardId, out var strategy) || strategy.Zone != Zone.FateDiscard) continue; // it has left the discard pile some other way
            var owner = Player(due.PlayerId);
            strategy.Zone = Zone.Hand; strategy.FaceUp = true; owner.Hand.Add(strategy.Id); strategy.AbilityUses.Clear();
            NoteCardIntoHand(owner.Id);
            Emit("returnedToHand", $"{Definition(strategy).Name} returns to {owner.Name}'s hand before the phase ends.", owner.Id, strategy.Id);
        }
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // null: not this feature's. false: the effects are done. true: a window opened (a deck search).
    private bool? RunPromoFate(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, HashSet<string> bowedBeforeAction)
    {
        switch (id)
        {
            case PromoEmperorFateAbilities.LegendaryRivalry:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case PromoEmperorFateAbilities.TamoriPotion:
            case PromoEmperorFateAbilities.BarFightAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case PromoEmperorFateAbilities.MightBow:
            case PromoEmperorFateAbilities.MightFree:
                DestroyTargetCard(target!, player.Id);
                return false;
            case PromoEmperorFateAbilities.Fushicho:
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case PromoEmperorFateAbilities.Glimpse:
            {
                // "You may discard a card": the chosen one (an effect, so it is skipped when the action is negated). Then draw until exactly two cards are held (the Strategy in play is not one).
                foreach (var discard in SlotCards(command, "discard").ToArray()) DiscardFate(player, discard);
                var held = player.Hand.Count(h => h != source!.Id);
                for (var i = held; i < 2; i++) Draw(player);
                Emit("glimpseOfFate", $"{player.Name} holds {player.Hand.Count(h => h != source!.Id)} card(s) in hand.", player.Id, source!.Id);
                return false;
            }
            case PromoEmperorFateAbilities.Adroitness:
                Straighten(target!);
                if (source is not null && source.Zone is Zone.Hand or Zone.FateDiscard)
                    PromoFate.PhaseReturns.Add(new PromoPhaseReturn(source.Id, player.Id, PhaseFamily(_state.Phase)));
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoEmperorFateHooks()
    {
        EngineHooks.Abilities.Register("AddPromoEmperorFate", 12100, static abilities => abilities.AddPromoEmperorFate());
        EngineHooks.TargetAllows.Register("PromoEmperorFateTargetAllows", 12100, static (engine, context) => engine.PromoFateTargetAllows(context.ability.Target, context.target));
        EngineHooks.ValidateLate.Register("ValidatePromoEmperorFate", 12100, static (engine, context) => engine.ValidatePromoFate(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunPromoEmperorFate", 12100, static (engine, context) => engine.RunPromoFate(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.bowedBeforeAction));
        EngineHooks.AttackStrengthAfter.Register("PromoEmperorFateAttackStrength", 12100, static (engine, context) => engine.PromoFateAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("PromoEmperorFateAttackKind", 12100, static context => PromoFateAttackKind(context.abilityId));
        EngineHooks.PersonalityForce.Register("PromoEmperorFateForce", 12100, static (engine, context) => engine.PromoMightForce(context.card));
        EngineHooks.BowsTarget.Register("PromoEmperorFateBowsTarget", 12100, static context => context.abilityId is PromoEmperorFateAbilities.LegendaryRivalry);
        EngineHooks.DestroysTarget.Register("PromoEmperorFateDestroysTarget", 12100, static context => context.abilityId is PromoEmperorFateAbilities.MightBow or PromoEmperorFateAbilities.MightFree);
    }
}
