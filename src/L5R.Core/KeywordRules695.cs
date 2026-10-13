using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P10 (issue #695; docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md, docs/decisions/2026-10-07-695-p10-keyword-rules.md): the keyword-rule registry.
// Engine only: no card is wired here.
//
// Keyword() was a fixed chain with a few per-card branches ("Naval while it has a Spell", Jun-Ni's unit Cavalry, Yayu's Cavalry when it is not your turn, Watari's sole Follower). Four pieces replace
// the need for a new branch each time a card prints a keyword rule:
//
//   1. KEYWORD RULES. EngineHooks.KeywordRule is consulted ONCE inside Keyword(), just before the printed and granted keywords are read, with the card and the keyword asked about; it answers
//      Granted, Suppressed (a Suppressed answer beats a printed or granted keyword, never a permanent removal) or null. The catalogue below (KeywordRuleCatalog) is the data form: a KeywordRule
//      names its source (a card Effect or a definition id), a scope (Self, Unit, Army, Controller, All; in and out of play when asked), the keywords, and conditions. The conditions reuse P9's
//      TermCondition for the source card ("while another player is Clan X") and add the ones about the card the keyword is asked of (KeywordCondition). Rules are read live, every call: nothing is
//      cached on a card, so a rule applies and lapses with its source and its condition.
//   2. KEYWORD GAIN BLOCKS ("will not gain Fallen or Shadowlands", "will not gain a Clan Alignment"): a stored record honoured by GrantKeyword, the permanent copies (PermanentCopy.cs, CardCopy.cs), a
//      created card's controller-Clan grant and the Clan-alignment grant paths (ClanAlignments); a block with a source card ends with it (pruned live, so a source that left play and came back does not
//      revive its block).
//   3. ELEMENT REPLACEMENT ("replace all instances of that keyword on this Shugenja with a different element keyword"): a stored map read by Keyword(), so every element read (Spell eligibility, "a Fire
//      Shugenja", Ring-style checks) sees it. The elements the card has WITHOUT replacements are computed first, then mapped.
//   4. PERMANENT CLAN ALIGNMENT: GrantClanAlignment writes a permanent grant read by Keyword() ("Crane Clan") and ClanAlignments (Loyalty, Unaligned, the Clan discount); the existing
//      RemoveClanAlignments removes granted alignments too. Permanent here means for the rest of the game, like a permanent copy (written outside the per-stay resets).
//
// Re-entrancy: a rule being evaluated for a (card, rule) counts as nothing inside its own evaluation (the P9 convention), so two rules that read each other's keywords cannot loop. Hot path: with
// no catalogue rule for the keyword and nothing stored, a Keyword() call adds one dictionary lookup and a handful of integer tests, and allocates nothing.

/// <summary>What a keyword rule says about one keyword on one card.</summary>
internal enum KeywordRuleResult { Granted, Suppressed }

internal enum KeywordRuleScope
{
    /// <summary>The card that carries the rule (a keyword it has itself, in or out of play unless a condition says otherwise).</summary>
    Self,
    /// <summary>Every card in the source's unit: its Personality and the cards attached to him (the source is the Personality or one of those cards).</summary>
    Unit,
    /// <summary>Every card in the source's army: the Personalities (and their attachments) at the same battlefield on the same side.</summary>
    Army,
    /// <summary>Every card the source's controller controls.</summary>
    Controller,
    /// <summary>Every card of every player.</summary>
    All,
}

internal enum KeywordRuleEffect { Grant, Suppress }

internal enum KeywordConditionKind
{
    /// <summary>The card has the keyword Arg (read live).</summary>
    SubjectHasKeyword,
    /// <summary>The card does not have the keyword Arg.</summary>
    SubjectLacksKeyword,
    /// <summary>The card is Human: a Personality without Nonhuman.</summary>
    SubjectIsHuman,
    /// <summary>The card's type is Arg ("Personality", "Follower", ...).</summary>
    SubjectIsType,
    /// <summary>The card has the Clan alignment Arg ("Crane").</summary>
    SubjectIsClan,
    /// <summary>The card's controller's Stronghold Clan is Arg.</summary>
    ControllerIsClan,
    /// <summary>An attachment of type Arg is attached to the card ("Spell").</summary>
    SubjectHasAttachmentOfType,
    /// <summary>It is not the card's controller's turn.</summary>
    NotControllersTurn,
    /// <summary>Exactly Arg Followers are attached to the source.</summary>
    SourceFollowerCount,
    /// <summary>The source's controller controls a Ring with the very keyword being asked about ("the element keywords of every Ring you control").</summary>
    ControllerControlsRingWithAskedKeyword,
    /// <summary>The card is in play.</summary>
    SubjectInPlay,
    /// <summary>The card is attached to another card.</summary>
    SubjectAttached,
    /// <summary>The card is the challenger of the pending duel the ability Arg issued ("during the duel", Focus on the Flame).</summary>
    SubjectChallengesWithAbility,
}

internal readonly record struct KeywordCondition(KeywordConditionKind Kind, string? Arg = null);

/// <summary>
/// One keyword rule. The source is the card carrying the rule: the cards whose Effect is SourceEffect, or whose definition id is SourceCardId (exactly one is named). AllOf must all hold for the
/// card the keyword is asked of; SourceAnyOf (P9's conditions, read on the source) needs any one of them when present. InAndOutOfPlay lets a non-Self rule reach cards that are not in play
/// ("in and out of play"); ExcludeSource leaves out the source itself ("your other Personalities").
/// </summary>
internal sealed record KeywordRule(string Id, KeywordRuleScope Scope, string[] Keywords, string? SourceEffect = null, string? SourceCardId = null, KeywordRuleEffect Effect = KeywordRuleEffect.Grant,
    KeywordCondition[]? AllOf = null, TermCondition[]? SourceAnyOf = null, bool InAndOutOfPlay = false, bool ExcludeSource = false);

/// <summary>The registered keyword rules, indexed by keyword. A card's feature registers its rules once; a test registers fixture rules and unregisters them.</summary>
internal static class KeywordRuleCatalog
{
    private static readonly object Gate = new();
    private static KeywordRule[] all = [];
    private static Dictionary<string, KeywordRule[]> byKeyword = new(StringComparer.Ordinal);

    internal static IReadOnlyList<KeywordRule> All => Volatile.Read(ref all);

    /// <summary>The rules that name this keyword, or null when none does (the common case: Keyword() returns at once).</summary>
    internal static KeywordRule[]? For(string keyword) => Volatile.Read(ref byKeyword).TryGetValue(keyword, out var found) ? found : null;

    internal static void Register(params KeywordRule[] rules)
    {
        lock (Gate)
        {
            var next = all.ToList();
            foreach (var rule in rules)
            {
                Validate(rule);
                if (next.Exists(existing => existing.Id == rule.Id)) throw new InvalidOperationException($"Duplicate keyword rule id: {rule.Id}");
                next.Add(rule);
            }
            Publish(next);
        }
    }

    internal static void Unregister(params string[] ids)
    {
        lock (Gate) Publish(all.Where(rule => !ids.Contains(rule.Id, StringComparer.Ordinal)).ToList());
    }

    private static void Publish(List<KeywordRule> rules)
    {
        var index = new Dictionary<string, KeywordRule[]>(StringComparer.Ordinal);
        foreach (var rule in rules)
            foreach (var keyword in rule.Keywords)
                index[keyword] = index.TryGetValue(keyword, out var existing) ? [.. existing, rule] : [rule];
        Volatile.Write(ref all, rules.ToArray());
        Volatile.Write(ref byKeyword, index);
    }

    private static void Validate(KeywordRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Id)) throw new InvalidOperationException("A keyword rule needs an id.");
        if (rule.Keywords.Length == 0) throw new InvalidOperationException($"{rule.Id}: a keyword rule names at least one keyword.");
        if ((rule.SourceEffect is null) == (rule.SourceCardId is null)) throw new InvalidOperationException($"{rule.Id}: name exactly one of SourceEffect and SourceCardId.");
        if (rule.SourceEffect is "" or "none") throw new InvalidOperationException($"{rule.Id}: a rule's source Effect must be a real card Effect.");
        if (rule.Scope == KeywordRuleScope.Self && rule.InAndOutOfPlay) throw new InvalidOperationException($"{rule.Id}: a Self rule is already read in and out of play; use SubjectInPlay to limit it.");
    }
}

// ---- stored state ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/// <summary>"Will not gain ...": CardId (one card) or PlayerId (every Personality that player controls, and the Personalities he creates) will not gain a keyword in Keywords, or of a class in Classes
/// ("clan": any "X Clan" keyword and alignment, "element": Air, Earth, Fire, Water or Void, "any"). It holds while SourceCardId is in play (null: for the rest of the game).</summary>
public sealed record KeywordGainBlock(string Id, string? SourceCardId, string? CardId, int? PlayerId, string[] Keywords, string[] Classes);

/// <summary>"Replace all instances of the element keyword From on this card with To": it holds while SourceCardId remains in play and attached to CardId.</summary>
public sealed record ElementReplacement(string Id, string CardId, string From, string To, string? SourceCardId);

/// <summary>A Clan alignment a card was permanently given (Suikihime's Sanction).</summary>
public sealed record ClanGrant(string CardId, string Clan, string Source);

public sealed class KeywordRulesState
{
    public List<KeywordGainBlock> Blocks { get; set; } = [];
    public List<ElementReplacement> ElementReplacements { get; set; } = [];
    public List<ClanGrant> ClanGrants { get; set; } = [];
    public int NextId { get; set; }
}

public sealed partial class MatchState
{
    // 22500-22509 is reserved for P10.
    [JsonPropertyOrder(22500)]
    public KeywordRulesState KeywordRules { get; set; } = new();
}

internal static partial class EngineHooks
{
    /// <summary>Consulted once by Keyword() for the card and keyword asked about: Granted, Suppressed (beats a printed or granted keyword) or null when the registrant has nothing to say.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.KeywordRuleContext, KeywordRuleResult?>> KeywordRule = new();
}

public sealed partial class MatchEngine
{
    internal readonly record struct KeywordRuleContext(CardInstance card, string keyword);

    private static readonly string[] ElementKeywordList = ["Air", "Earth", "Fire", "Water", "Void"];
    private static bool IsElementKeyword(string keyword) => keyword is "Air" or "Earth" or "Fire" or "Water" or "Void";

    // The (card, rule) pairs being evaluated right now: a rule inside its own evaluation counts as nothing (the P9 convention). Never saved: empty whenever no keyword is being read.
    private readonly HashSet<(string CardId, string RuleId)> _keywordRulesEvaluating = [];
    // While the elements a card has WITHOUT its replacements are being computed, replacements on that card are not applied.
    private string? _elementBypassCardId;

    private KeywordRulesState KeywordRulesStore => _state.KeywordRules;

    /// <summary>Keyword()'s one call: the first Suppressed answer wins, then any Granted; null when no registrant has an answer.</summary>
    private KeywordRuleResult? ConsultKeywordRules(CardInstance card, string keyword)
    {
        KeywordRuleResult? found = null;
        var context = new KeywordRuleContext(card, keyword);
        foreach (var hook in EngineHooks.KeywordRule.Items)
        {
            var answer = hook(this, context);
            if (answer == KeywordRuleResult.Suppressed) return answer;
            if (answer == KeywordRuleResult.Granted) found = answer;
        }
        return found;
    }

    // The registered callback: stored state first (cheap integer tests), then the catalogue.
    private KeywordRuleResult? KeywordRulesAnswer(CardInstance card, string keyword)
    {
        var store = _state.KeywordRules;
        if (store.Blocks.Count > 0 || store.ElementReplacements.Count > 0) PruneKeywordRuleState();
        if (store.ElementReplacements.Count > 0 && IsElementKeyword(keyword) && ElementReplacementAnswer(card, keyword) is { } replaced) return replaced;
        if (store.Blocks.Count > 0 && KeywordGainBlocked(card, keyword) && !PrintedKeyword(card, keyword) && !HasGrantedKeyword(card, keyword)) return KeywordRuleResult.Suppressed;
        if (store.ClanGrants.Count > 0 && keyword.EndsWith(" Clan", StringComparison.Ordinal) && ClanGranted(card, keyword[..^" Clan".Length])) return KeywordRuleResult.Granted;
        return KeywordRuleCatalog.For(keyword) is { } rules ? EvaluateKeywordRules(card, keyword, rules) : null;
    }

    private static bool HasGrantedKeyword(CardInstance card, string keyword)
    {
        foreach (var granted in card.GrantedKeywords)
            if (granted.Keyword == keyword) return true;
        return false;
    }

    // ---- the catalogue ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private KeywordRuleResult? EvaluateKeywordRules(CardInstance subject, string keyword, KeywordRule[] rules)
    {
        KeywordRuleResult? result = null;
        foreach (var rule in rules)
        {
            if (!KeywordRuleApplies(rule, subject, keyword)) continue;
            if (rule.Effect == KeywordRuleEffect.Suppress) return KeywordRuleResult.Suppressed;
            result = KeywordRuleResult.Granted;
        }
        return result;
    }

    private static bool KeywordRuleSourceMatches(KeywordRule rule, CardInstance card) =>
        rule.SourceEffect is { } effect ? Definition(card).Effect == effect : Definition(card).Id == rule.SourceCardId;

    private bool KeywordRuleApplies(KeywordRule rule, CardInstance subject, string keyword)
    {
        if (rule.Scope == KeywordRuleScope.Self) return KeywordRuleSourceMatches(rule, subject) && !SpellStaticSilenced(subject) && KeywordRuleHolds(rule, subject, subject, keyword);
        if (!rule.InAndOutOfPlay && !InPlay(subject)) return false;
        CardInstance? subjectHead = null;
        if (rule.Scope is KeywordRuleScope.Unit or KeywordRuleScope.Army)
        {
            subjectHead = _state.Cards[UnitHostId(subject)];
            if (rule.Scope == KeywordRuleScope.Army && (subjectHead.Zone != Zone.Battlefield || subjectHead.ProvinceIndex is null)) return false;
        }
        foreach (var source in _state.Cards.Values)
        {
            if (rule.ExcludeSource && source.Id == subject.Id) continue;
            // Cheap structural tests first (a few integer or reference-like comparisons): this loop runs for every Keyword() call about a keyword such a rule names.
            if (rule.Scope == KeywordRuleScope.Unit && source.Id != subjectHead!.Id && source.AttachedTo != subjectHead.Id) continue;
            if (rule.Scope is KeywordRuleScope.Army or KeywordRuleScope.Controller && source.OwnerId != (rule.Scope == KeywordRuleScope.Army ? subjectHead!.OwnerId : subject.OwnerId)) continue;
            if (!InPlay(source) || !KeywordRuleSourceMatches(rule, source) || SpellStaticSilenced(source)) continue; // R-085: a Shugenja-subject Spell on a non-Shugenja gives nothing
            var covered = rule.Scope switch
            {
                KeywordRuleScope.Unit => UnitHostId(source) == subjectHead!.Id,
                KeywordRuleScope.Army => KeywordRuleSameArmy(_state.Cards[UnitHostId(source)], subjectHead!),
                KeywordRuleScope.Controller => source.OwnerId == subject.OwnerId,
                _ => true
            };
            if (covered && KeywordRuleHolds(rule, source, subject, keyword)) return true;
        }
        return false;
    }

    private static bool KeywordRuleSameArmy(CardInstance sourceHead, CardInstance subjectHead) =>
        sourceHead.Zone == Zone.Battlefield && subjectHead.Zone == Zone.Battlefield && sourceHead.OwnerId == subjectHead.OwnerId && sourceHead.ProvinceIndex == subjectHead.ProvinceIndex;

    private bool KeywordRuleHolds(KeywordRule rule, CardInstance source, CardInstance subject, string keyword)
    {
        var key = (subject.Id, rule.Id);
        if (!_keywordRulesEvaluating.Add(key)) return false;
        try
        {
            if (rule.SourceAnyOf is { Length: > 0 } sourceConditions)
            {
                var any = false;
                foreach (var condition in sourceConditions)
                    if (StatTermConditionHolds(source, condition)) { any = true; break; }
                if (!any) return false;
            }
            if (rule.AllOf is not null)
                foreach (var condition in rule.AllOf)
                    if (!KeywordConditionHolds(condition, source, subject, keyword)) return false;
            return true;
        }
        finally { _keywordRulesEvaluating.Remove(key); }
    }

    private bool KeywordConditionHolds(KeywordCondition condition, CardInstance source, CardInstance subject, string asked)
    {
        switch (condition.Kind)
        {
            case KeywordConditionKind.SubjectHasKeyword: return Keyword(subject, condition.Arg!);
            case KeywordConditionKind.SubjectLacksKeyword: return !Keyword(subject, condition.Arg!);
            case KeywordConditionKind.SubjectIsHuman: return Definition(subject).Type == CardType.Personality && KeywordOrHuman(subject, "Human");
            case KeywordConditionKind.SubjectIsType: return Definition(subject).Type.ToString() == condition.Arg;
            case KeywordConditionKind.SubjectIsClan: return HasClanAffiliation(subject, condition.Arg!);
            case KeywordConditionKind.ControllerIsClan: return ControllerClans(subject.OwnerId).Contains(condition.Arg!, StringComparer.Ordinal);
            case KeywordConditionKind.SubjectHasAttachmentOfType:
                foreach (var other in _state.Cards.Values)
                    if (other.Zone == Zone.Attached && other.AttachedTo == subject.Id && Definition(other).Type.ToString() == condition.Arg) return true;
                return false;
            case KeywordConditionKind.NotControllersTurn: return subject.OwnerId != _state.ActivePlayerId;
            case KeywordConditionKind.SourceFollowerCount:
                {
                    var count = 0;
                    foreach (var other in _state.Cards.Values)
                        if (other.Zone == Zone.Attached && other.AttachedTo == source.Id && Definition(other).Type == CardType.Follower) count++;
                    return count.ToString(System.Globalization.CultureInfo.InvariantCulture) == condition.Arg;
                }
            case KeywordConditionKind.ControllerControlsRingWithAskedKeyword:
                foreach (var other in _state.Cards.Values)
                    if (other.OwnerId == source.OwnerId && InPlay(other) && Definition(other).Type == CardType.Ring && Keyword(other, asked)) return true;
                return false;
            case KeywordConditionKind.SubjectInPlay: return InPlay(subject);
            case KeywordConditionKind.SubjectAttached: return subject.Zone == Zone.Attached && subject.AttachedTo is not null;
            case KeywordConditionKind.SubjectChallengesWithAbility: return _state.PendingDuel is { } duel && duel.AbilityId == condition.Arg && duel.ChallengerId == subject.Id;
            default: throw new InvalidOperationException($"Unknown keyword condition: {condition.Kind}");
        }
    }

    // ---- gain blocks -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool KeywordGainMatches(KeywordGainBlock block, string keyword)
    {
        foreach (var named in block.Keywords)
            if (named == keyword) return true;
        foreach (var kind in block.Classes)
            if (kind == "any" || kind == "clan" && keyword.EndsWith(" Clan", StringComparison.Ordinal) || kind == "element" && IsElementKeyword(keyword)) return true;
        return false;
    }

    /// <summary>True when a standing block says the card will not gain the keyword. Blocks are read live; the card's printed keywords are never taken away by a block.</summary>
    internal bool KeywordGainBlocked(CardInstance card, string keyword)
    {
        var blocks = _state.KeywordRules.Blocks;
        if (blocks.Count == 0) return false;
        foreach (var block in blocks)
        {
            if (!BlockHolds(block)) continue;
            var covers = block.CardId == card.Id || block.PlayerId == card.OwnerId && Definition(card).Type == CardType.Personality;
            if (covers && KeywordGainMatches(block, keyword)) return true;
        }
        return false;
    }

    // "Will not gain a Clan alignment" for a card that does not exist yet (a created Personality that would take his creator's Clan): only a player-level block can speak for it.
    private bool CreatedClanGainBlocked(int playerId, string clan)
    {
        foreach (var block in _state.KeywordRules.Blocks)
            if (BlockHolds(block) && block.PlayerId == playerId && KeywordGainMatches(block, clan + " Clan")) return true;
        return false;
    }

    private bool BlockHolds(KeywordGainBlock block) =>
        block.SourceCardId is null || _state.Cards.TryGetValue(block.SourceCardId, out var source) && InPlay(source);

    /// <summary>"Will not gain ..." for CardId (or, with a player id and no card, every Personality he controls): until SourceCardId leaves play when it is named. Returns the block's id.</summary>
    internal string AddKeywordGainBlock(string? sourceCardId, string? cardId, int? playerId, string[]? keywords = null, string[]? classes = null)
    {
        if ((cardId is null) == (playerId is null)) throw new InvalidOperationException("A keyword gain block names a card or a player.");
        var store = KeywordRulesStore;
        PruneKeywordRuleState();
        var id = "keyword-block-" + (++store.NextId);
        store.Blocks.Add(new(id, sourceCardId, cardId, playerId, keywords ?? [], classes ?? []));
        return id;
    }

    internal void RemoveKeywordGainBlocks(string sourceCardId) => KeywordRulesStore.Blocks.RemoveAll(block => block.SourceCardId == sourceCardId);

    // ---- element replacement ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Replace all instances of the element keyword `from` on this card with `to`": Keyword() answers for the mapped elements while the source stays in play (and attached to the card).</summary>
    internal string ReplaceElementKeyword(string cardId, string from, string to, string? sourceCardId)
    {
        if (!IsElementKeyword(from) || !IsElementKeyword(to) || from == to) throw new InvalidOperationException("An element replacement maps one element keyword to a different one.");
        var store = KeywordRulesStore;
        PruneKeywordRuleState();
        var id = "element-replacement-" + (++store.NextId);
        store.ElementReplacements.Add(new(id, cardId, from, to, sourceCardId));
        return id;
    }

    internal void RemoveElementReplacements(string sourceCardId) => KeywordRulesStore.ElementReplacements.RemoveAll(r => r.SourceCardId == sourceCardId);

    private KeywordRuleResult? ElementReplacementAnswer(CardInstance card, string keyword)
    {
        if (_elementBypassCardId == card.Id) return null;
        List<ElementReplacement>? mine = null;
        foreach (var replacement in _state.KeywordRules.ElementReplacements)
            if (replacement.CardId == card.Id && !(replacement.SourceCardId is { } blood && _state.Cards.TryGetValue(blood, out var bloodSpell) && SpellStaticSilenced(bloodSpell))) (mine ??= []).Add(replacement); // R-085: Blood of Isawa's Tribe is silent on a non-Shugenja
        if (mine is null) return null;
        var previous = _elementBypassCardId;
        _elementBypassCardId = card.Id;
        try
        {
            foreach (var element in ElementKeywordList)
            {
                if (!Keyword(card, element)) continue;
                var current = element;
                foreach (var replacement in mine)
                    if (replacement.From == current) current = replacement.To;
                if (current == keyword) return KeywordRuleResult.Granted;
            }
            return KeywordRuleResult.Suppressed;
        }
        finally { _elementBypassCardId = previous; }
    }

    // ---- Clan alignment ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool ClanGranted(CardInstance card, string clan)
    {
        foreach (var grant in _state.KeywordRules.ClanGrants)
            if (grant.CardId == card.Id && grant.Clan == clan) return true;
        return false;
    }

    // The alignments ClanAlignments adds to a card's printed ones: those a static rule gives it (Seppun Nagi, Atosa) and those it was permanently given, minus any a block forbids.
    private string[] GainedClanAlignments(CardInstance card)
    {
        var gained = StaticClanAlignments(card);
        var store = _state.KeywordRules;
        if (store.ClanGrants.Count == 0 && store.Blocks.Count == 0) return gained;
        var all = new List<string>(gained);
        foreach (var grant in store.ClanGrants)
            if (grant.CardId == card.Id && !all.Contains(grant.Clan)) all.Add(grant.Clan);
        if (store.Blocks.Count > 0) all.RemoveAll(clan => KeywordGainBlocked(card, clan + " Clan"));
        return all.ToArray();
    }

    /// <summary>Permanently gives the card a Clan alignment ("Crane"): false when a block says it will not gain it, true otherwise (also when it already has it). A removed alignment comes back.</summary>
    internal bool GrantClanAlignment(CardInstance card, string clan, string source)
    {
        if (KeywordGainBlocked(card, clan + " Clan"))
        {
            Emit("effectNegated", $"{Definition(card).Name} will not gain the {clan} Clan alignment.", card.OwnerId, card.Id);
            return false;
        }
        card.RemovedClanAffiliations.RemoveAll(c => c == clan);
        card.RemovedKeywords.RemoveAll(k => k == clan + " Clan");
        var store = KeywordRulesStore;
        store.ClanGrants.RemoveAll(grant => grant.CardId == card.Id && grant.Clan == clan);
        if (ClanAlignments(card).Contains(clan, StringComparer.Ordinal)) return true;
        store.ClanGrants.Add(new(card.Id, clan, source));
        Emit("clanAlignmentGranted", $"{Definition(card).Name} permanently gains the {clan} Clan alignment.", card.OwnerId, card.Id);
        return true;
    }

    /// <summary>Permanently removes ONE Clan alignment (printed, static or granted): the existing RemoveClanAlignments' bookkeeping for a single Clan.</summary>
    internal void RemoveClanAlignment(CardInstance card, string clan)
    {
        KeywordRulesStore.ClanGrants.RemoveAll(grant => grant.CardId == card.Id && grant.Clan == clan);
        if (!ClanAlignments(card).Contains(clan, StringComparer.Ordinal)) return;
        card.RemovedClanAffiliations.Add(clan);
        if (!card.RemovedKeywords.Contains($"{clan} Clan", StringComparer.Ordinal)) card.RemovedKeywords.Add($"{clan} Clan");
        Emit("clanAlignmentRemoved", $"{Definition(card).Name} permanently loses the {clan} Clan alignment.", card.OwnerId, card.Id);
    }

    // ---- sweeping ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // A block or replacement whose source card has left play (or, for a replacement, is no longer attached to the card) is gone for good: removed here rather than left to revive when the source returns.
    private void PruneKeywordRuleState()
    {
        var store = _state.KeywordRules;
        for (var i = store.Blocks.Count - 1; i >= 0; i--)
            if (store.Blocks[i].SourceCardId is { } id && !(_state.Cards.TryGetValue(id, out var source) && InPlay(source))) store.Blocks.RemoveAt(i);
        for (var i = store.ElementReplacements.Count - 1; i >= 0; i--)
        {
            var replacement = store.ElementReplacements[i];
            if (replacement.SourceCardId is { } id && !(_state.Cards.TryGetValue(id, out var source) && InPlay(source) && (source.Zone != Zone.Attached || source.AttachedTo == replacement.CardId)))
                store.ElementReplacements.RemoveAt(i);
        }
    }

    /// <summary>The existing RemoveClanAlignments' companion: every permanent grant to the card goes with the alignments it removed.</summary>
    internal void ForgetClanGrants(CardInstance card) => KeywordRulesStore.ClanGrants.RemoveAll(grant => grant.CardId == card.Id);

    // ---- test seams (L5R.Core.Tests has InternalsVisibleTo) -----------------------------------------------------------------------------------------------------------------------------------

    internal void GrantKeywordForTest(string cardId, string keyword, string source, string duration) => GrantKeyword(Card(cardId), keyword, source, duration);
    internal void PermanentlyCopyPersonalityForTest(string copierId, string fromId) => PermanentlyCopyPersonality(Card(copierId), Card(fromId));
    internal void PermanentlyCopySpellForTest(string copierId, string spellId) => PermanentlyCopySpell(Card(copierId), Card(spellId));
    internal string[] ClanAlignmentsForTest(string cardId) => ClanAlignments(Card(cardId));
    internal void RemoveClanAlignmentsForTest(string cardId) => RemoveClanAlignments(Card(cardId));
    internal CardDefinition WithControllerClanForTest(CardDefinition printed, int playerId) => WithControllerClan(printed, Player(playerId));
    internal void RemoveKeywordGainBlocksForTest(string sourceCardId) => RemoveKeywordGainBlocks(sourceCardId);
    internal bool IsCavalryUnitForTest(string personalityId) => IsCavalryUnit(Card(personalityId));
    internal bool AttachmentLegalTargetForTest(string attachmentId, string targetId) => AttachmentLegalTarget(Definition(Card(attachmentId)), Card(targetId));
    internal bool ConsultKeywordRulesAnswersForTest(string cardId, string keyword) => ConsultKeywordRules(Card(cardId), keyword) is not null;
    internal bool GrantClanAlignmentForTest(string cardId, string clan, string source) => GrantClanAlignment(Card(cardId), clan, source);
    internal void RemoveClanAlignmentForTest(string cardId, string clan) => RemoveClanAlignment(Card(cardId), clan);
    internal string AddKeywordGainBlockForTest(string? sourceCardId, string? cardId, int? playerId, string[]? keywords = null, string[]? classes = null) => AddKeywordGainBlock(sourceCardId, cardId, playerId, keywords, classes);
    internal string ReplaceElementKeywordForTest(string cardId, string from, string to, string? sourceCardId) => ReplaceElementKeyword(cardId, from, to, sourceCardId);

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterKeywordRuleHooks() =>
        EngineHooks.KeywordRule.Register("P10KeywordRules", 16500, static (engine, context) => engine.KeywordRulesAnswer(context.card, context.keyword));
}
