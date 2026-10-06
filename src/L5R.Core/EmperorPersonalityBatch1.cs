namespace L5R.Core;

// Emperor Edition Personality pass (issue #477), batch 1 (personality-wave-68): execution helpers of the Spider/Shadowlands Personalities this batch wires. The declarative data is in
// CardAbilities.cs; each new primitive is documented at its own use here.
//
//   * ALL OF A CARD'S ABILITIES MAY NOT BE USED (Ninube Onchi): the single-ability lock (MatchState.DisabledAbilities, ForgottenLegacyBatch1.cs) with a wildcard usage key, so every action
//     printed on or granted to the Personality is refused until the turn ends -- the same lock, read wherever an action is judged performable (RequireCanPerform).
//   * A STATIC "NEGATE MOVEMENT TO MY BATTLEFIELD" TRAIT (Ugulu no Oni): read in MoveToBattlefield, the one function every movement to a battlefield goes through (Kyuden Kitsune's shape).
//   * "FOLLOWERS ATTACHING TO [X] ENTER PLAY PAYING LESS GOLD" (Daigotsu Misaki): a term of the equip price (GoldQuotes.EquipGoldCost), keyed on the wielder's Effect.
public sealed partial class MatchEngine
{
    /// <summary>The usage key that stands for every ability of a card in a <see cref="DisabledAbility"/>.</summary>
    private const string AllAbilitiesUsageKey = "*";

    private const string UguluNegatesHumanMovementEffect = "ugulu-negate-human-movement";
    private const string MisakiFollowerDiscountEffect = "misaki-follower-discount";

    // Misaki: "Followers attaching to Misaki enter play paying 2 less Gold" -- every attach of a Follower to her (from a hand or a discard pile alike).
    private static int FollowerAttachDiscount(CardInstance card, CardInstance wielder) =>
        Definition(wielder).Effect == MisakiFollowerDiscountEffect && Definition(card).Type == CardType.Follower ? 2 : PromoHorseAttachDiscount(card, wielder); // Horse ignores its cost attaching to Kataoka (issue #653)

    // Misaki: "Melee Attack with strength equal to the Follower's Force" -- sourceId is the performing Follower (ResolveOrDeferAttack's source), read live.
    private int? EmperorPersonalityBatch1AttackStrength(string abilityId, string? sourceId) =>
        abilityId == "misaki-follower-attack" ? sourceId is not null ? CardForce(Card(sourceId)) : 0 : null;

    // Ugulu no Oni: "Before a Human Personality moves to Ugulu's battlefield: Negate his movement." Asked by MoveToBattlefield before the card moves; a Personality already at that battlefield does
    // not move to it, and a Personality that is not Human (Nonhuman) is not affected. Ugulu's own controller's Humans are negated too ("a Human Personality").
    private bool UguluNegatesMovement(CardInstance card, int province)
    {
        if (Definition(card).Type != CardType.Personality || !KeywordOrHuman(card, "Human")) return false;
        var ugulu = _state.Cards.Values.FirstOrDefault(c => c.Id != card.Id && c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Definition(c).Effect == UguluNegatesHumanMovementEffect);
        if (ugulu is null || MovementNegationBarred(card, ugulu.OwnerId)) return false; // Winter Sled (issue #667): the negating card is Ugulu's.
        Emit("moveNegated", $"{Definition(card).Name}'s movement to {Definition(ugulu).Name}'s battlefield was negated.", card.OwnerId, card.Id);
        return true;
    }

    // An entering-play Reaction whose target is not in play (Aya's Dark Virtue in a discard pile): the listing offers these candidates instead of the in-play cards; null for every other category.
    private IEnumerable<string>? OutOfPlayEntryReactionTargets(AbilityDefinition ability, int playerId) => ability.Target switch
    {
        "own-dark-virtue-card-in-discard" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Strategy && Keyword(c, "Dark Virtue") && c.Zone == Zone.FateDiscard).Select(c => c.Id),
        _ => null
    };

    private void RemoveAllAbilities(CardInstance card, string source)
    {
        _state.DisabledAbilities.RemoveAll(d => d.Turn != _state.TurnNumber);
        _state.DisabledAbilities.Add(new(card.Id, AllAbilitiesUsageKey, _state.TurnNumber, source));
        Emit("abilitiesRemoved", $"{source}: {Definition(card).Name}'s abilities are removed until the turn ends.", card.OwnerId, card.Id);
    }

    private bool RunEmperorPersonalityBatch1Effect(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            // Daigotsu Aya: the optional Dark Virtue goes from her controller's discard pile to his hand.
            case "aya-entry-dark-virtue":
                ReturnToHand(target!);
                return false;
            // Aya: "Give Aya Cavalry, Duelist, or Tactician" -- for the turn (core rules, Duration).
            case "aya-cavalry": GrantKeyword(source!, "Cavalry", source!.Id, "Turn"); return false;
            case "aya-duelist": GrantKeyword(source!, "Duelist", source!.Id, "Turn"); return false;
            case "aya-tactician": GrantKeyword(source!, "Tactician", source!.Id, "Turn"); return false;
            // Daigotsu Negishi: "Straighten him."
            case "negishi-straighten":
                Straighten(target!);
                return false;
            // Daigotsu Misaki: the performing Follower's bow was paid as its cost option; the Follower then performs the Attack.
            case "misaki-follower-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId!, consumedGrant);
            // Ninube Onchi: none of the target's actions may be used until the turn ends.
            case "onchi-remove-abilities":
                RemoveAllAbilities(target!, Definition(source!).Name);
                return false;
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition batch 1 ability '{id}'.");
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorPersonalityBatch1Hooks()
    {
        EngineHooks.AttackStrengthBefore.Register("EmperorPersonalityBatch1AttackStrength", 600, static (engine, context) => engine.EmperorPersonalityBatch1AttackStrength(context.abilityId, context.sourceId));
    }
}
