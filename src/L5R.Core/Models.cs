using System.Text.Json.Serialization;

namespace L5R.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardType { Stronghold, Holding, Personality, Strategy, Follower, Item, Spell, Event, Celestial, Region, Ring }
[JsonConverter(typeof(JsonStringEnumConverter))]
// AttackDeclaration/DefenseAssignment is the Infantry Maneuvers segment; CavalryDeclaration/
// CavalryDefense is the Cavalry Maneuvers segment that follows it (core rules Attack Phase, step C).
public enum Phase { Straighten, Events, Action, AttackDeclaration, DefenseAssignment, CavalryDeclaration, CavalryDefense, BattleSelection, Battle, Dynasty, DynastyDiscard, End, Finished }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Zone { Stronghold, Home, Province, DynastyDeck, FateDeck, Hand, DynastyDiscard, FateDiscard, Dead, Battlefield, Attached }

public sealed record CardDefinition(string Id, string Name, CardType Type, string Clan = "Unaligned", int GoldCost = 0,
    int GoldProduction = 0, int Force = 0, int Chi = 0, int PersonalHonor = 0, int? HonorRequirement = null,
    int ProvinceStrength = 0, int StartingHonor = 0, string Effect = "none", string Text = "",
    bool ScenarioOnly = true, string? ArtworkKey = null, string[]? Keywords = null,
    string[]? Abilities = null, int Focus = 0,
    // Overrides Clan for a Personality aligned with more than one Clan; null means "just Clan".
    string[]? Clans = null,
    // An attachment's own "Will only/not attach to X" restriction (Item/Follower/Spell alike).
    // AttachRequiresKeyword/AttachExcludesKeyword usually name a printed keyword, checked against the
    // target Personality -- "Human" is a special case (core rules Glossary: no keyword is ever
    // printed for it, so it means "does not have Nonhuman"). AttachRequiresName is the rarer
    // named-Personality restriction (e.g. "Attaches to Yotsu Ueda").
    string? AttachRequiresKeyword = null, string? AttachExcludesKeyword = null, string? AttachRequiresName = null,
    // "This Personality has X" (Commander's Steed's shape): an attachment that grants its wielder a
    // keyword for as long as it stays attached -- reuses the existing GrantedKeyword("Attachment")
    // lifecycle (already cleaned up on detach/destroy; see DestroyAttachment), just never exercised by
    // a published card until now.
    string? AttachGrantsKeyword = null,
    // Seppun Heavy Elite's shape: grants its wielder the EFFECT Elite has (Force still counts while
    // bowed, see BattleForce's EliteEffect() check) without actually granting the Elite keyword
    // itself -- a real keyword grant would satisfy any future ability that checks specifically for
    // the printed Elite keyword (the same printed-vs-granted distinction Justice of the Crane's own
    // text draws for Magistrate). Deliberately not folded into AttachGrantsKeyword/GrantedKeywords.
    bool AttachGrantsEliteEffect = false,
    // Passive Ranged/Melee Attack-strength modifier applied to an incoming Attack TARGETING the
    // scoped card(s) -- Destrier's shape ("Melee and Ranged Attacks targeting this Follower have
    // -3 strength"), generalized. AttackDefenseKind is "Melee", "Ranged", or "Both" (null: grants
    // none); AttackDefenseAmount is signed (negative protects, matching every printed card seen so
    // far, but nothing here assumes it can't be positive, e.g. a card that makes attacks easier to
    // land against it). AttackDefenseScope is "Self" (only this card itself, Destrier's own shape),
    // "Wielder" (an Item's default when its text does not say "unit" -- conveys to the Personality
    // wielding it, not the Item itself), or "Unit" (every card in the unit -- Singh Remnants'/
    // Elephant Cavalry's shape). Which scope applies is decided per card when it is wired, from its
    // own printed text -- the engine does not infer it from Type.
    string? AttackDefenseKind = null, int AttackDefenseAmount = 0, string AttackDefenseScope = "Self");

/// <summary>One applied Force/Chi change, in application order, so a future card can negate a
/// specific prior effect instead of only ever resetting a flat running total.</summary>
public sealed record AppliedModifier(string Source, string Effect, string Stat, int Amount);

/// <summary>A keyword granted by another card rather than printed. Duration is "Attachment"
/// (lasts until that attachment moves or is destroyed), "Turn" (cleared at turn end, the default
/// for an action-granted keyword), or "Permanent" (only when the granting text says so).</summary>
public sealed record GrantedKeyword(string Keyword, string Source, string Duration);

/// <summary>A physical marker on a card, not a card itself (core rules Glossary — Token). Type
/// identifies the kind of token (e.g. "Fire", "Madness") for other effects to count/reference; it
/// does not grant its host a keyword. Stat/Amount, when set, is a persistent bonus/penalty carried
/// by this specific token -- unlike a Modifier, it is not cleared at turn end, only when the token
/// itself is destroyed or its host card leaves play.</summary>
public sealed record CardToken(string Id, string Type, string Source, string? Stat = null, int Amount = 0);

public sealed class CardInstance
{
    public string Id { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public int OwnerId { get; set; }
    public Zone Zone { get; set; }
    public int? ProvinceIndex { get; set; }
    public bool FaceUp { get; set; }
    public bool Bowed { get; set; }
    public string? AttachedTo { get; set; }
    public List<AppliedModifier> Modifiers { get; set; } = [];
    public List<GrantedKeyword> GrantedKeywords { get; set; } = [];
    public List<CardToken> Tokens { get; set; } = [];
    public bool Dishonorable { get; set; }
    public int StraightenBlockedTurn { get; set; } = -1;
    public Dictionary<string, int> AbilityUses { get; set; } = [];
}

public sealed class ProvinceState
{
    public int Index { get; set; }
    public int Strength { get; set; }
    public bool Destroyed { get; set; }
    public string? CardId { get; set; }
    // Reconnaissance (core rules Glossary): a per-player boolean at this specific province, indexed
    // by player id, defaulted to false. No ability removes it directly; it resets to false for both
    // players only once a battle at this province resolves (ResolveBattle), regardless of outcome.
    public bool[] Reconnaissance { get; set; } = new bool[2];
}

public sealed class PlayerState
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Clan { get; set; } = "";
    public int Honor { get; set; }
    public int TurnsTaken { get; set; }
    public int ProclaimUsedTurn { get; set; } = -1;
    public bool DishonorLossPending { get; set; }
    public Dictionary<string, int> AbilityUses { get; set; } = [];
    public string StrongholdId { get; set; } = "";
    public List<string> DynastyDeck { get; set; } = [];
    public List<string> FateDeck { get; set; } = [];
    public List<string> Hand { get; set; } = [];
    public List<ProvinceState> Provinces { get; set; } = [];
}

public sealed record PendingChoice(string Type, int PlayerId, string? CardId, string Prompt, int? ActingPlayerId = null);
public sealed record GameEvent(long Sequence, string Type, string Message, int? PlayerId = null, string? CardId = null, int? Amount = null);

/// <summary>A Ranged/Melee Attack awaiting resolution, open to reaction (core rules: "players must
/// have an opportunity to react"). Strength is deliberately mutable state, not a one-shot value --
/// either player's Reaction may raise, lower, or (by driving it to 0 or below) negate it before
/// Kind/Strength are compared against the target's current Force at actual resolution time.</summary>
public sealed class PendingAttack
{
    public string AbilityId { get; set; } = "";
    public int PlayerId { get; set; }
    // The card that made the attack, when a card-specific post-attack effect (e.g. Tsuruchi Isas'
    // "Move Isas home") needs the exact instance rather than just its controller.
    public string? SourceId { get; set; }
    public string? TargetId { get; set; }
    public int Strength { get; set; }
    public string Kind { get; set; } = "";
}

/// <summary>Conqueror (core rules Glossary): open only when the attacker won and more than one of
/// their attacking Personalities carries the Conqueror keyword -- a real choice for the attacker.
/// (Exactly one candidate resolves immediately, with no pending state, since there is nothing to
/// choose.) ProvinceIndex is enough to recompute the untouched attacking army once the choice lands;
/// nothing about it can change while this is the only legal next command (see ResolveConqueror).</summary>
public sealed class PendingConqueror
{
    public int PlayerId { get; set; }
    public int ProvinceIndex { get; set; }
    public string[] CandidateIds { get; set; } = [];
}

public sealed class MatchState
{
    public long Version { get; set; }
    public int TokenSequence { get; set; }
    public int? FavorPlayerId { get; set; }
    public int TurnNumber { get; set; } = 1;
    public int ActivePlayerId { get; set; }
    public int PriorityPlayerId { get; set; }
    public Phase Phase { get; set; }
    public int ConsecutivePasses { get; set; }
    public PendingChoice? PendingChoice { get; set; }
    public PendingAttack? PendingAttack { get; set; }
    public PendingConqueror? PendingConqueror { get; set; }
    public int? CurrentBattleProvince { get; set; }
    public List<int> UnresolvedBattlefields { get; set; } = [];
    // Ashigaru Elite's shape: a per-player counter of the current battle's Force boost to each of the
    // performer's own Ashigaru-keyword Personalities/Followers at this battlefield, incremented once
    // per use (stacks, tireless). Reset to zero whenever a battle resolves (FinishBattleResolution) --
    // "for the remainder of the battle," not the turn -- since a turn can contain more than one battle.
    public int[] AshigaruBoost { get; set; } = new int[2];
    public int? WinnerId { get; set; }
    public string? VictoryReason { get; set; }
    public PlayerState[] Players { get; set; } = [];
    public Dictionary<string, CardInstance> Cards { get; set; } = [];
    public List<GameEvent> Events { get; set; } = [];
}

public sealed record GameCommand(string Type, int PlayerId, long ExpectedVersion, string? CardId = null,
    string[]? SourceIds = null, int? Province = null, bool ClanDiscount = false, bool ClanAllegiance = false,
    Dictionary<string, int>? Assignments = null, string[]? CardIds = null, string? TargetId = null, string? AbilityId = null, string? PerformerId = null, string? Choice = null);
public sealed record CommandResult(bool Accepted, string? Error, long Version, GameEvent[] Events);
public sealed record PaymentOption(string[] SourceIds, int Produced, int Cost, int Excess);
public sealed record PurchaseQuote(string CardId, int Cost, bool ClanDiscount, bool ClanAllegiance,
    bool ProclaimEligible, string? Error, PaymentOption[] Payments);
public sealed record ActionOption(string Type, string Label, bool Enabled, string? Reason,
    GameCommand? Command = null, string? CardId = null, string[]? TargetIds = null,
    int[]? Provinces = null, PurchaseQuote? Quote = null);
/// <param name="UnitForce">Authoritative effective unit contribution, including qualifying Followers.
/// Null for hidden/non-unit/out-of-play cards and older payloads; never substitute a client calculation.</param>
public sealed record CardView(string Id, CardDefinition? Definition, int OwnerId, Zone Zone, bool FaceUp,
    bool Bowed, int? ProvinceIndex, string? AttachedTo, int Force, int Chi = 0, bool StraightenBlocked = false,
    AppliedModifier[]? Modifiers = null, GrantedKeyword[]? GrantedKeywords = null, bool Dishonorable = false, int? UnitForce = null,
    CardToken[]? Tokens = null);
public sealed record ProvinceView(int Index, int Strength, bool Destroyed, CardView? Card);
public sealed record PlayerView(int Id, string Name, string Clan, int Honor, int TurnsTaken,
    CardView Stronghold, CardView[] Home, CardView[] Army, ProvinceView[] Provinces,
    CardView[] Hand, int HandCount, int DynastyDeckCount, int FateDeckCount, CardView[] Discard,
    bool ProclaimUsedThisTurn, bool DishonorLossPending);
public sealed record MatchView(string ContentVersion, string Scenario, long Version, int TurnNumber,
    int ViewerId, int ActivePlayerId, int PriorityPlayerId, Phase Phase, PendingChoice? PendingChoice,
    int? CurrentBattleProvince, int[] UnresolvedBattlefields, int? WinnerId, string? VictoryReason,
    PlayerView[] Players, GameEvent[] Events, string[] Limitations, int? FavorPlayerId = null);
public sealed record ReplayRecord(string ContentVersion, string Scenario, ulong Seed, GameCommand[] Commands);
