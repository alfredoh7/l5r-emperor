namespace L5R.Core;

// Static Force for the cards at a Personality's location (Before the Dawn, issue #431). Hiruma Akio - exp: "Your Followers at Akio's location have +1F while you have Reconnaissance." A Follower is at its
// host's location; a Personality's location is Home, or a battlefield. It is a trait of the card that carries it (its Effect), read live for every Follower of its controller, so it is gone the moment
// Akio leaves play or Reconnaissance is lost.
public sealed partial class MatchEngine
{
    private int AkioFollowerBoost(CardInstance follower)
    {
        if (follower.Zone != Zone.Attached || follower.AttachedTo is not string hostId || !_state.Cards.TryGetValue(hostId, out var host)) return 0;
        if (!HasReconnaissanceAtCurrentBattlefield(follower.OwnerId)) return 0;
        return _state.Cards.Values.Count(a => a.OwnerId == follower.OwnerId && InPlay(a) && Definition(a).Effect == "akio-follower-force" && SameLocation(a, host));
    }

    internal int MaxHandSizeForTest(int playerId) => MaxHandSize(playerId);
    internal int PersonalityChiForTest(string cardId) => PersonalityChi(Card(cardId));
    internal int AttackStrengthForTest(string abilityId, string sourceId) => AttackStrength(abilityId, Player(Card(sourceId).OwnerId), null, sourceId);
}
