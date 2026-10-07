using System.Collections.Concurrent;
using System.Text;

namespace L5R.Core;

/// <summary>What an ability's printed text says the card it creates is; an omitted stat is not compared. Item Force/Chi are the printed "+2F/+0C" bonuses, stored unsigned.
/// Keywords is the exact set (order irrelevant); AbilityText is compared with the scan's text ignoring case and punctuation; ProxyId names the scan when the rest fits several.
/// Count is how many cards the ability creates; an attachment goes to a chosen host (the command's CardIds, or its TargetId): with PerHost, Count of them to EACH chosen host
/// ("one to each of one to three Personalities"), otherwise Count in all, dealt to the chosen hosts in turn ("two Followers, attached to one or two Personalities").
/// AtBattlefield creates a Personality at the current battlefield instead of at home. UntilBattleEnds removes the created cards from the game when the battle ends.
/// Or lists the other cards the player may choose instead ("an Armor or Weapon Item"): each is a spec of its own, and the command names the scan chosen (GameCommand.CreatedCard).
/// Count applies to a Personality too: that many are created together. HostIsPerformer attaches a created attachment to the action's performer ("attach them to him").
/// ControllerClan gives the created Personality its creator's Stronghold Clan and Clan keyword, fixed when it is created ("with your Clan alignment"). Values set printed values of
/// the one created card from another card ("the performer's title and base Gold Cost", "Force equal to the target's Personal Honor"); the scan is still what the card is.
/// EntersBowed creates a Holding bowed ("Create a (bowed) Holding"); a Holding enters play at home, a Terrain (a Strategy with the Terrain keyword) at the current battlefield.
/// P22 (issue #690): RemoveWhen removes the created cards from the game at a later moment; Abilities and Trait declare what the created card prints beyond its stats.</summary>
public sealed record CreatedCardSpec(CardType Type, int? Force = null, int? Chi = null, int? PersonalHonor = null, string[]? Keywords = null,
    string? AbilityText = null, string? ProxyId = null, int Count = 1, bool PerHost = false, bool AtBattlefield = false, bool UntilBattleEnds = false, CreatedCardSpec[]? Or = null, bool CopyOfTarget = false)
{
    public bool HostIsPerformer { get; init; }
    /// <summary>Attaches a created attachment to the creating card itself ("attach it to him" on an entering-play Reaction of the Personality: Masayoshi, issue #453).</summary>
    public bool HostIsSource { get; init; }
    /// <summary>Attach the created Item to the Personality wearing the source Item.</summary>
    public bool HostIsWielder { get; init; }
    /// <summary>Attaches a created attachment to the Personality whose entry opened the window the Reaction is used in ("After your target Personality enters play: Create ... and attach it to him": Shinjo Ki-Chang - exp, issue #492 proxy group Y).</summary>
    public bool HostIsEntered { get; init; }
    public bool ControllerClan { get; init; }
    public bool EntersBowed { get; init; }
    /// <summary>The slot whose cards are the hosts of a created attachment, when it is not the ability's first card slot (Goju Kobashi: "target one of your Ninja ... attach it to him" is his SECOND target;
    /// Emperor Edition proxy pass, issue #492).</summary>
    public string? HostSlot { get; init; }
    /// <summary>"You may target ..., create ...": with no host chosen nothing is created and the action still resolves (Goju Kobashi).</summary>
    public bool HostOptional { get; init; }
    /// <summary>Count is the target's Chi as it stood before the action's costs were paid ("a number of Followers equal to his Chi", Suiteiru no Oni: the target is destroyed as a cost).</summary>
    public bool CountFromTargetChi { get; init; }
    public CreatedValue[]? Values { get; init; }
    /// <summary>P22 (issue #690): when the created cards are removed from the game ("Remove it from the game after your next turn begins", "After the next time this game this Spell
    /// straightens or leaves play, remove the Illusion from the game"). Null: they stay until something else removes them.</summary>
    public CreatedRemoval? RemoveWhen { get; init; }
    /// <summary>P22 (issue #690): the abilities the created card prints ("... Personality with the ability, 'Battle, Bow: Melee 4 Attack.'"), by ability id, written on the one created card
    /// beside the abilities its scan's catalogue record already lists. The scan must print them: AbilityText is required and compared with the scan's text (CreatedCards.Verify).
    /// A mandatory triggered trait is an ability id registered as a trait (CreatedCopies690Catalog.RegisterTrait).</summary>
    public string[]? Abilities { get; init; }
    /// <summary>P22 (issue #690): the continuous trait the created card prints ("with the trait ..."), as the Effect id its rules are keyed on (CardDefinition.Effect), written on the one
    /// created card. The scan must print it: AbilityText is required.</summary>
    public string? Trait { get; init; }

    public bool HasInstanceValues => ControllerClan || Values is { Length: > 0 } || Abilities is { Length: > 0 } || Trait is not null;

    public IEnumerable<CreatedCardSpec> Alternatives => Or is { Length: > 0 } others ? others.Prepend(this with { Or = null }) : [this];

    public string Describe()
    {
        var text = new StringBuilder(Type.ToString());
        if (Force is not null || Chi is not null || PersonalHonor is not null)
            text.Append(' ').Append(string.Join('/', new[] { Force is int f ? $"{f}F" : null, Chi is int c ? $"{c}C" : null, PersonalHonor is int p ? $"{p}PH" : null }.Where(s => s is not null)));
        if (Keywords is not null) text.Append(" [").Append(string.Join(", ", Keywords)).Append(']');
        if (AbilityText is not null) text.Append(" with \"").Append(AbilityText).Append('"');
        if (ProxyId is not null) text.Append(" (scan ").Append(ProxyId).Append(')');
        if (CopyOfTarget) text.Append(" copying the target");
        if (Count != 1) text.Append(PerHost ? $" x{Count} per host" : $" x{Count}");
        if (AtBattlefield) text.Append(" at the battlefield");
        if (UntilBattleEnds) text.Append(" until the battle ends");
        if (EntersBowed) text.Append(" bowed");
        if (HostIsPerformer) text.Append(" attached to the performer");
        if (HostIsSource) text.Append(" attached to the source");
        if (HostIsWielder) text.Append(" attached to the source's wielder");
        if (HostIsEntered) text.Append(" attached to the entering Personality");
        if (ControllerClan) text.Append(" with the controller's Clan");
        if (Values is { Length: > 0 } values) text.Append(" with ").Append(string.Join(", ", values.Select(v => v.Describe())));
        if (Abilities is { Length: > 0 } abilities) text.Append(abilities.Length == 1 ? " with the ability " : " with the abilities ").Append(string.Join(", ", abilities));
        if (Trait is not null) text.Append(" with the trait ").Append(Trait);
        if (RemoveWhen is not null) text.Append(", removed ").Append(RemoveWhen.Describe());
        if (Or is { Length: > 0 } others) text.Append(" or ").Append(string.Join(" or ", others.Select(o => o.Describe())));
        return text.ToString();
    }
}

/// <summary>Focus (P22, issue #690) is read only: the Focus Value of the subject, never a value a created card is given.</summary>
public enum CreatedStat { Name, GoldCost, Force, Chi, PersonalHonor, Focus }

/// <summary>The card a created card's value is read from: the action's performer, its source card, the Personality its source is attached to, or its target.
/// Slot (P22, issue #690): the first card the command names for the ability's slot CreatedValue.Slot ("the discarded card": a cost-only slot).</summary>
public enum CreatedSubject { Performer, Source, Wielder, Target, Slot }

/// <summary>One printed value of a created card, taken from another card's value: Stat is set from Read of From (the same stat when Read is omitted) plus Plus, and plus
/// PlusWithKeyword more when the subject has Keyword. Name is copied as it is. The numbers are the subject's PRINTED values unless Current is set (P22, issue #690): then they
/// are its values as they stand when the card is created (its Force, Chi, Personal Honor or Gold Cost with its attachments, tokens and modifiers), written on the created
/// card once and never following the subject afterwards.</summary>
public sealed record CreatedValue(CreatedStat Stat, CreatedSubject From, CreatedStat? Read = null, int Plus = 0, string? Keyword = null, int PlusWithKeyword = 0)
{
    /// <summary>Read the subject's current value instead of its printed one ("with Force equal to this Shugenja's Chi").</summary>
    public bool Current { get; init; }
    /// <summary>The slot whose first card is the subject, when From is Slot ("equal to the discarded card's Focus Value").</summary>
    public string? Slot { get; init; }

    public string Describe() => $"{Stat} from the {(From == CreatedSubject.Slot ? $"card in slot {Slot}" : From.ToString().ToLowerInvariant())}"
        + (Read is { } read && read != Stat ? $"'s {(Current ? "current " : "")}{read}" : Current ? " (current)" : "")
        + (Plus != 0 ? $" {Plus:+#;-#}" : "") + (Keyword is null ? "" : $" and {PlusWithKeyword:+#;-#} with {Keyword}");
}

/// <summary>P22 (issue #690): when a created card is removed from the game. When is a TimedRemovalWhen value (the turn ends; the creating player's next turn begins), or
/// SourceStraightensOrLeaves: after the next time this game the action's source card (the Spell, the Holding, the Personality the ability is on) straightens or leaves play.</summary>
public sealed record CreatedRemoval(string When)
{
    public const string SourceStraightensOrLeaves = "source-straightens-or-leaves";

    /// <summary>"Remove it from the game when the turn ends."</summary>
    public static readonly CreatedRemoval TurnEnds = new(TimedRemovalWhen.TurnEnds);
    /// <summary>"Remove it from the game after your next turn begins": the creating player's.</summary>
    public static readonly CreatedRemoval NextTurnBegins = new(TimedRemovalWhen.PlayersNextTurnBegins);
    /// <summary>"After the next time this game this Spell straightens or leaves play, remove the Illusion from the game."</summary>
    public static readonly CreatedRemoval WhenSourceStraightensOrLeaves = new(SourceStraightensOrLeaves);

    public bool IsWatch => When == SourceStraightensOrLeaves;

    public string Describe() => When switch
    {
        TimedRemovalWhen.TurnEnds => "when the turn ends",
        TimedRemovalWhen.PlayersNextTurnBegins => "when the creating player's next turn begins",
        SourceStraightensOrLeaves => "after its source next straightens or leaves play",
        _ => $"at an unknown moment ({When})"
    };
}

/// <summary>A spec that no scan, or more than one scan, fits. Candidates are the scans that matched or, when none did, the scans of the same type.</summary>
public sealed class CreatedCardResolutionException(CreatedCardSpec spec, string reason, CardDefinition[] candidates)
    : InvalidOperationException($"Created card {spec.Describe()}: {reason}")
{
    public CreatedCardSpec Spec { get; } = spec;
    public CardDefinition[] Candidates { get; } = candidates;
}

/// <summary>Finds the Proxy scan (a published CardDefinition with CreatedOnly) that a "create a card" ability's spec describes.</summary>
public static class CreatedCards
{
    private static readonly ConcurrentDictionary<CreatedCardSpec, CardDefinition> Resolved = new(ReferenceEqualityComparer.Instance);

    public static IEnumerable<CardDefinition> Scans => CardCatalog.Cards.Values.Where(c => c.CreatedOnly).OrderBy(c => c.Id, StringComparer.Ordinal);

    public static bool Creatable(CardType type) => type is CardType.Personality or CardType.Item or CardType.Follower or CardType.Spell or CardType.Holding or CardType.Strategy;

    public static bool IsTerrain(CardDefinition scan) => scan.Type == CardType.Strategy && (scan.Keywords ?? []).Contains("Terrain");

    public static bool IsAttachment(CardType type) => type is CardType.Item or CardType.Follower or CardType.Spell;

    public static CardDefinition Resolve(CreatedCardSpec spec) => spec.CopyOfTarget ? throw new InvalidOperationException("A created copy has no scan: it copies its target.") : Resolved.TryGetValue(spec, out var known) ? known : Resolved[spec] = Resolve(spec, Scans);

    internal static CardDefinition Resolve(CreatedCardSpec spec, IEnumerable<CardDefinition> scans)
    {
        var all = scans.Where(c => c.CreatedOnly).ToArray();
        var pool = all;
        if (spec.ProxyId is string named)
        {
            pool = all.Where(c => c.Id == named).ToArray();
            if (pool.Length == 0) throw new CreatedCardResolutionException(spec, $"{named} is not a created-only card in the catalogue", []);
        }
        var matches = pool.Where(c => Fits(spec, c)).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) throw new CreatedCardResolutionException(spec, $"{matches.Length} scans fit it ({string.Join(", ", matches.Select(c => $"{c.Name} {c.Id}"))}); name one with ProxyId", matches);
        throw new CreatedCardResolutionException(spec, spec.ProxyId is null ? "no created-only scan fits it" : $"{spec.ProxyId} does not fit it",
            spec.ProxyId is null ? all.Where(c => c.Type == spec.Type).ToArray() : pool);
    }

    private static bool Fits(CreatedCardSpec spec, CardDefinition scan) =>
        scan.Type == spec.Type
        && (spec.Force is null || scan.Force == spec.Force) && (spec.Chi is null || scan.Chi == spec.Chi) && (spec.PersonalHonor is null || scan.PersonalHonor == spec.PersonalHonor)
        && (spec.Keywords is null || new HashSet<string>(scan.Keywords ?? [], StringComparer.OrdinalIgnoreCase).SetEquals(spec.Keywords))
        && (spec.AbilityText is null || Normalize(scan.Text).Contains(Normalize(spec.AbilityText), StringComparison.Ordinal));

    private static string Normalize(string text)
    {
        var words = new StringBuilder();
        foreach (var c in text) words.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        return string.Join(' ', words.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool CanHave(CardType type, CreatedStat stat) => stat switch
    {
        CreatedStat.Name or CreatedStat.GoldCost => true,
        CreatedStat.Force => type is CardType.Personality or CardType.Follower or CardType.Item,
        CreatedStat.Chi => type is CardType.Personality or CardType.Item,
        CreatedStat.Focus => false,
        _ => type == CardType.Personality
    };

    // P22 (issue #690): a removal moment the engine knows.
    private static void VerifyRemoval(string abilityId, CreatedCardSpec spec, List<string> problems)
    {
        if (spec.RemoveWhen is { } removal && removal.When is not (TimedRemovalWhen.TurnEnds or TimedRemovalWhen.PlayersNextTurnBegins or CreatedRemoval.SourceStraightensOrLeaves))
            problems.Add($"{abilityId}: {spec.Describe()} is removed at a moment the engine does not know");
    }

    // A Personality carries one Armor and one Weapon (two with Kensai): several created at once could not all attach, and are not built.
    private static bool IsSingleOfKind(CardDefinition scan) => scan.Type == CardType.Item && (scan.Keywords ?? []).Any(k => k is "Armor" or "Weapon");

    /// <summary>The alternative the command chose: the only one when there is only one, else the one whose scan id the command names (null when it names none of them).
    /// A copy-of-target alternative has no scan to name, so a command that names one chooses none of them -- validation then rejects it as an illegal action
    /// (Resolve would throw InvalidOperationException, which is not a RuleViolation and would end the session instead).</summary>
    public static CreatedCardSpec? Chosen(CreatedCardSpec spec, string? chosenScanId)
    {
        var options = spec.Alternatives.ToArray();
        if (chosenScanId is null) return options.Length == 1 ? options[0] : null;
        return options.FirstOrDefault(o => !o.CopyOfTarget && Resolve(o).Id == chosenScanId);
    }

    public static IEnumerable<(string AbilityId, CreatedCardSpec Spec)> Creators =>
        AbilityCatalog.All.Where(p => p.Value.Creates is not null).Select(p => (p.Key, p.Value.Creates!));

    /// <summary>One line per creator whose spec cannot be resolved (no scan, several scans, a kind the engine cannot create yet); empty when every creator has its scan.</summary>
    public static string[] Verify() => Verify(Creators, Scans);

    internal static string[] Verify(IEnumerable<(string AbilityId, CreatedCardSpec Spec)> creators, IEnumerable<CardDefinition> scans)
    {
        var problems = new List<string>();
        var all = scans.ToArray();
        foreach (var (abilityId, spec) in creators)
        {
            // A copy of the ability's target has no scan to find: what it copies is chosen with the action. Only a Personality is copied so far.
            if (spec.CopyOfTarget)
            {
                if (spec.Type != CardType.Personality) problems.Add($"{abilityId}: a created copy of a {spec.Type} is not built");
                else if (spec.Force is not null || spec.Chi is not null || spec.PersonalHonor is not null || spec.Keywords is not null || spec.AbilityText is not null || spec.ProxyId is not null)
                    problems.Add($"{abilityId}: {spec.Describe()} names stats or a scan, but a copy is exactly the card it copies");
                if (spec.Abilities is { Length: > 0 } || spec.Trait is not null) problems.Add($"{abilityId}: {spec.Describe()} declares an ability or trait, but a copy is exactly the card it copies");
                VerifyRemoval(abilityId, spec, problems);
                continue;
            }
            foreach (var alternative in spec.Alternatives)
                try
                {
                    var scan = Resolve(alternative, all);
                    if (!Creatable(scan.Type) || scan.Type == CardType.Strategy && !IsTerrain(scan)) problems.Add($"{abilityId}: {scan.Name} ({scan.Id}) is a {scan.Type}, which the engine cannot create yet");
                    if (alternative.Count > 1 && (scan.Type == CardType.Holding || IsTerrain(scan))) problems.Add($"{abilityId}: {alternative.Describe()} creates several {scan.Name}, and only one Holding or Terrain is created at a time");
                    if (alternative.EntersBowed && scan.Type != CardType.Holding) problems.Add($"{abilityId}: {alternative.Describe()} enters {scan.Name} bowed, and only a Holding does");
                    if (alternative.Count < 1 || alternative.AtBattlefield && scan.Type != CardType.Personality) problems.Add($"{abilityId}: {alternative.Describe()} names a count or a place that {scan.Name} cannot have");
                    if (alternative.Count > 1 && IsSingleOfKind(scan)) problems.Add($"{abilityId}: {alternative.Describe()} creates several {scan.Name}, a card a Personality carries only one of");
                    if (alternative.Count > 1 && scan.Type == CardType.Personality && (scan.Keywords ?? []).Contains("Unique")) problems.Add($"{abilityId}: {alternative.Describe()} creates several {scan.Name}, a Unique card");
                    if (alternative.HostIsPerformer && !IsAttachment(scan.Type)) problems.Add($"{abilityId}: {alternative.Describe()} attaches {scan.Name}, which is not an attachment");
                    if (alternative.HostIsSource && (!IsAttachment(scan.Type) || alternative.HostIsPerformer)) problems.Add($"{abilityId}: {alternative.Describe()} attaches {scan.Name} to its source, which needs an attachment and no performer host");
                    if (alternative.HostIsWielder && (!IsAttachment(scan.Type) || alternative.HostIsSource || alternative.HostIsPerformer)) problems.Add($"{abilityId}: {alternative.Describe()} attaches {scan.Name} to its wielder, which needs an attachment and no other host");
                    var namesPerformer = alternative.HostIsPerformer || (alternative.Values ?? []).Any(v => v.From == CreatedSubject.Performer);
                    if (namesPerformer && AbilityCatalog.All.TryGetValue(abilityId, out var creator) && creator.Performer is null) problems.Add($"{abilityId}: {alternative.Describe()} names the performer, and the ability has none");
                    if (alternative.HostIsEntered && (!IsAttachment(scan.Type) || alternative.HostIsPerformer || alternative.HostIsSource
                            || !AbilityCatalog.All.TryGetValue(abilityId, out var enterer) || enterer.Trigger is not ("own-personality-enters-play" or EmperorRulingR3Abilities.EntersFromProvinceTrigger)))
                        problems.Add($"{abilityId}: {alternative.Describe()} attaches {scan.Name} to the entering Personality, which needs an attachment, no other host and an own-personality-enters-play Reaction");
                    if (alternative.ControllerClan && scan.Type != CardType.Personality) problems.Add($"{abilityId}: {alternative.Describe()} gives {scan.Name} a Clan, which only a Personality has");
                    foreach (var value in alternative.Values ?? [])
                    {
                        if (!CanHave(scan.Type, value.Stat) || value.Stat == CreatedStat.Name && (value.Read is not null || value.Plus != 0 || value.Keyword is not null || value.Current) || value.PlusWithKeyword != 0 != (value.Keyword is not null))
                            problems.Add($"{abilityId}: {alternative.Describe()} sets {value.Stat} of {scan.Name} in a way it cannot be set");
                        // P22 (issue #690): a slot subject names a card slot of the ability, and only a slot subject names one.
                        if (value.From == CreatedSubject.Slot != (value.Slot is not null)
                            || value.Slot is not null && AbilityCatalog.All.TryGetValue(abilityId, out var slotted) && slotted.Slots?.Any(s => s.Id == value.Slot && !s.IsPlayer) != true)
                            problems.Add($"{abilityId}: {alternative.Describe()} reads {value.Stat} of {scan.Name} from a slot the ability does not have");
                    }
                    // P22 (issue #690): a declared ability or trait is one the scan prints (AbilityText is what is compared with its text), and the ability exists.
                    if ((alternative.Abilities is { Length: > 0 } || alternative.Trait is not null) && alternative.AbilityText is null)
                        problems.Add($"{abilityId}: {alternative.Describe()} declares an ability or trait of {scan.Name} without the printed text (AbilityText) the scan is held to");
                    foreach (var declared in alternative.Abilities ?? [])
                        if (!AbilityCatalog.All.ContainsKey(declared)) problems.Add($"{abilityId}: {alternative.Describe()} declares the ability {declared}, which is not catalogued");
                    if (alternative.Trait is { } trait && scan.Effect != "none" && scan.Effect != trait)
                        problems.Add($"{abilityId}: {alternative.Describe()} declares the trait {trait}, but {scan.Name} already has the trait {scan.Effect}");
                    VerifyRemoval(abilityId, alternative, problems);
                }
                catch (CreatedCardResolutionException e) { problems.Add($"{abilityId}: {e.Message}"); }
        }
        return problems.ToArray();
    }
}
