namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), proxy group X: the three cards that were blocked only because the Proxy card they create had no scan -- Goju Kobashi (Kobashi's Spawn, oracle-l5r-10008),
// Suiteiru no Oni (Suiteiru's Podling, oracle-l5r-10021) and the Spell Summon Swamp Spirits (Swamp Spirit, oracle-l5r-10014). The scans are local now and the Proxies ship in their parent's wave.
//
// The declarative ability data is here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file.
//
// New primitives (each reads the core rules; none conflicts with them):
//   * CreatedCardSpec.HostSlot / HostOptional: a created attachment goes to the cards of a NAMED slot (not the ability's first one), and "You may target one of your Ninja, create ... and attach it to him" creates
//     nothing, and still resolves, when the optional slot is left empty (Goju Kobashi).
//   * CreatedCardSpec.CountFromTargetChi: "a number of Followers equal to his Chi" -- the target's Chi read BEFORE the action's costs destroy it (CaptureBeforeCostsForEmperorProxyX; Suiteiru no Oni).
//   * TargetSlot.Repeatable: a slot that may name the same card more than once, each naming one share ("attach them to one or more of your Personalities": a host named twice takes two Followers; a shorter
//     list is dealt round-robin, so naming each host once splits the Followers evenly).
public static class EmperorProxyXAbilities
{
    /// <summary>"One of your Ninja" (Goju Kobashi): one of the acting player's own Ninja Personalities in play, Kobashi himself included.</summary>
    public const string OwnNinjaPersonality = "proxyx-own-ninja-personality";
    /// <summary>"One or more of your Personalities" other than the one the action destroys as its cost (Suiteiru no Oni): read against the target slot chosen before it, as Bayushi Nitoshi's second target is.</summary>
    public const string OwnPersonalityOtherThanTarget = "proxyx-own-personality-other-than-target";

    public static Dictionary<string, AbilityDefinition> AddEmperorProxyX(this Dictionary<string, AbilityDefinition> all)
    {
        // Goju Kobashi (oracle-l5r-2853, a Consumer): "After Kobashi enters play: Lose 1 Honor." is his EntryHonorLoss. "Battle: Target an enemy attachment: Kobashi consumes it. Destroy it. You may target one of your
        // Ninja, create a 1F Shadowlands - Ninja Follower, and attach it to him." Both targets are named up front (the second optional); "consumes" has no rules of its own (Consumer is a keyword with no text),
        // so the sentence "Kobashi consumes it. Destroy it." is the destruction. The Follower is Kobashi's Spawn (oracle-l5r-10008).
        all["kobashi-consume"] = new("Battle", "Target an enemy attachment: Kobashi consumes it. Destroy it. You may target one of your Ninja, create a 1F Shadowlands · Ninja Follower, and attach it to him", "slots",
            Slots: [new("attachment", "enemy-attachment", "An enemy attachment to consume and destroy"), new("ninja", OwnNinjaPersonality, "One of your Ninja, to attach a created Follower to (optional)", 0, 1)],
            Creates: new(CardType.Follower, Force: 1, Keywords: ["Shadowlands", "Ninja"], ProxyId: "oracle-l5r-10008") { HostSlot = "ninja", HostOptional = true });
        // Suiteiru no Oni (oracle-l5r-7618): "After Suiteiru enters play: Lose 4 Honor." is his EntryHonorLoss. "Open: Destroy your target unbowed Personality: Create a number of 1F Shadowlands - Nonhuman - Oni Followers
        // equal to his Chi. Attach them to one or more of your Personalities. Lose Honor equal to the number of Followers this created." The destruction is a cost (before the colon), so the Chi is read first; the
        // hosts are a repeatable slot of your Personalities other than the destroyed one (at most one share per Follower). The Followers are Suiteiru's Podlings (oracle-l5r-10021).
        all["suiteiru-create"] = new("Open", "Destroy your target unbowed Personality: Create a number of 1F Shadowlands · Nonhuman · Oni Followers equal to his Chi. Attach them to one or more of your Personalities. Lose Honor equal to the number of Followers this created", "slots",
            Slots: [new("target", "own-unbowed-personality", "Your unbowed Personality to destroy"),
                new("hosts", OwnPersonalityOtherThanTarget, "One or more of your Personalities to attach the Followers to (name one twice for two Followers)", 1, 99) { Repeatable = true }],
            Costs: [new(CostVerbs.Sacrifice, CostSubject.Slot("target"))],
            Creates: new(CardType.Follower, Force: 1, Keywords: ["Shadowlands", "Nonhuman", "Oni"], ProxyId: "oracle-l5r-10021") { HostSlot = "hosts", CountFromTargetChi = true });
        // Summon Swamp Spirits (oracle-l5r-7633, a Maho Spell): "Limited: Bow this Shugenja and target your Personality: Create a 1F Shadowlands - Nonhuman - Spirit - Swamp Spirit Follower and attach it to him."
        // The Follower is the Swamp Spirit (oracle-l5r-10014), attached to the target (any of your Personalities, not only the Shugenja).
        all["summon-swamp-spirits-create"] = new("Limited", "Bow this Shugenja and target your Personality: Create a 1F Shadowlands · Nonhuman · Spirit · Swamp Spirit Follower and attach it to him", "own-personality",
            Costs: ActionCost.BowWielder, Creates: new(CardType.Follower, Force: 1, Keywords: ["Shadowlands", "Nonhuman", "Spirit", "Swamp Spirit"], ProxyId: "oracle-l5r-10014"));
        return all;
    }
}

public sealed partial class MatchEngine
{
    private int _createdCountFromTargetChi;

    private static bool IsEmperorProxyXAbility(string id) => id is "kobashi-consume" or "suiteiru-create";

    private bool EmperorProxyXTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        EmperorProxyXAbilities.OwnNinjaPersonality => target.OwnerId == player.Id && InPlay(target) && Definition(target).Type == CardType.Personality && Keyword(target, "Ninja"),
        // The destroyed target is still in play while the command is validated, so it is named out; the other Personalities are any of the acting player's own.
        EmperorProxyXAbilities.OwnPersonalityOtherThanTarget => target.OwnerId == player.Id && InPlay(target) && Definition(target).Type == CardType.Personality
            && !SlotCards(command, "target").Any(t => t.Id == target.Id),
        _ => false
    };

    // Suiteiru no Oni: the Followers are one per point of the target's Chi and every one of them is attached, so the hosts named are at most as many as the Followers (a host named twice takes two).
    private void ValidateEmperorProxyX(string id, GameCommand command)
    {
        if (id != "suiteiru-create") return;
        var victim = SlotCards(command, "target").FirstOrDefault();
        var hosts = SlotCards(command, "hosts").ToArray();
        if (victim is null) return;
        // The target is destroyed as a cost, so it is not one of "your Personalities" the Followers attach to.
        Require(hosts.All(h => h.Id != victim.Id), "The Personality destroyed as the cost cannot carry the Followers.");
        Require(hosts.Length <= Math.Max(1, PersonalityChi(victim)), "Each Follower goes to one Personality: name no more hosts than the target's Chi.");
    }

    // The Chi Suiteiru's Followers are counted from is read before the destruction cost removes the target (and its Chi modifiers) from play.
    private void CaptureBeforeCostsForEmperorProxyX(string id, GameCommand command)
    {
        if (id == "suiteiru-create" && SlotCards(command, "target").FirstOrDefault() is { } victim) _createdCountFromTargetChi = Math.Max(0, PersonalityChi(victim));
    }

    // true: an entering-play window opened and the action waits for it.
    private bool RunEmperorProxyXEffect(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            // Kobashi consumes the attachment (destroys it); the Ninja's Follower is created whether or not the destruction did anything (independent sentences), and only if a Ninja was named.
            case "kobashi-consume":
                DestroyAttachment(target!);
                return ExecuteCreate(ability, ability.Creates!, command, player);
            // Suiteiru: the target was destroyed as a cost; the Followers are created (their count is the captured Chi) and the Honor loss, equal to that many, is taken once they exist.
            case "suiteiru-create":
                return ExecuteCreate(ability, ability.Creates!, command, player, () => { if (_createdCountFromTargetChi > 0) GainHonor(player.Id, -_createdCountFromTargetChi, Definition(source!).Name, source); });
            default:
                throw new InvalidOperationException($"{id} is not a proxy group X ability.");
        }
    }
}
