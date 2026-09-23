namespace L5R.Core;

// Static keyword rules over a CATEGORY of a controller's cards (Seeds of Decay pass, issue #453, batch 4). A card in play carrying one of the
// Effects below grants, or takes away, keywords from every card of its controller's that its printed text describes -- read live by Keyword() on
// every call, like Tamori Yayu's own conditional Cavalry, never a durable GrantedKeyword (the rule is gone the moment its source leaves play, and it
// reaches cards that were not in play when it entered). The four Seeds of Decay shapes:
//   aimi-lion-courtier          Ikoma Aimi: "Your Lion Clan Personalities (in play) have Courtier but do not have Cavalry, Commander, Deathseeker, Paragon, Scout, or Tactician."
//   nagi-magistrate-clan        Seppun Nagi: "Your other non-Loyal Magistrate Personalities in and out of play have your Clan alignment." (the "<Clan> Clan" keyword and,
//                               through ClanAlignments, the alignment itself: the Clan discount, Loyal, "Unaligned")
//   nobukatsu-mantis-magistrate Tsuruchi Nobukatsu - exp: "Your Mantis Clan Samurai in and out of play have Magistrate."
//   loses-brash-vs-lion         Shinjo Katsuo: "Katsuo does not have Brash if any other player is Lion Clan." (self-scoped, Kitsuki Minori's loses-overconfident-vs-lion shape)
// "In and out of play" is every card the controller owns, wherever it is (a deck, a hand, a province, a discard pile); "(in play)" is only the cards in play.
// A rule's own scope test asks Keyword() about OTHER keywords of the same card (Nagi's "Magistrate", Nobukatsu's "Mantis Clan"), and two rules can ask
// about each other's keyword in a loop: the question being asked is remembered while it is asked, and a rule re-asked the same question inside its own
// answer contributes nothing (the printed keywords, and every other rule, still do).
public sealed partial class MatchEngine
{
    private const string AimiLionCourtierEffect = "aimi-lion-courtier";
    private const string NagiMagistrateClanEffect = "nagi-magistrate-clan";
    private const string NobukatsuMantisMagistrateEffect = "nobukatsu-mantis-magistrate";
    private const string LosesBrashVsLionEffect = "loses-brash-vs-lion";

    private static readonly string[] AimiSuppressedKeywords = ["Cavalry", "Commander", "Deathseeker", "Paragon", "Scout", "Tactician"];

    private readonly HashSet<(string CardId, string Keyword)> _staticKeywordQuestions = [];

    private static bool IsStaticKeywordRuleEffect(string effect) => effect is AimiLionCourtierEffect or NagiMagistrateClanEffect or NobukatsuMantisMagistrateEffect;

    // The Clans of a player's Stronghold ("your Clan alignment"), none for an Unaligned Stronghold.
    private string[] ControllerClans(int playerId)
    {
        var stronghold = Definition(Card(Player(playerId).StrongholdId));
        return (stronghold.Clans ?? [stronghold.Clan]).Where(clan => clan != "Unaligned").Distinct(StringComparer.Ordinal).ToArray();
    }

    private IEnumerable<CardInstance> StaticKeywordRuleSources(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && IsStaticKeywordRuleEffect(Definition(c).Effect));

    // Seppun Nagi's scope: another Personality of his controller's, a Magistrate, not Loyal -- wherever it is.
    private bool NagiCovers(CardInstance nagi, CardInstance card) =>
        card.Id != nagi.Id && Definition(card).Type == CardType.Personality && Keyword(card, "Magistrate") && !Keyword(card, "Loyal");

    // The Clan alignments a static rule gives this card (Nagi's), on top of its printed ones. Empty for nearly every card.
    private string[] StaticClanAlignments(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) return [];
        var key = (card.Id, "*clan*");
        if (!_staticKeywordQuestions.Add(key)) return [];
        try
        {
            foreach (var source in StaticKeywordRuleSources(card.OwnerId))
                if (Definition(source).Effect == NagiMagistrateClanEffect && NagiCovers(source, card)) return ControllerClans(card.OwnerId);
            return [];
        }
        finally { _staticKeywordQuestions.Remove(key); }
    }

    // Keyword()'s two questions: does a rule give this card the keyword, does a rule take it away. Cheap when the keyword is one no rule names.
    private bool StaticKeywordGranted(CardInstance card, string keyword) =>
        (keyword is "Courtier" or "Magistrate" || keyword.EndsWith(" Clan", StringComparison.Ordinal)) && AskStaticKeywordRules(card, keyword, granted: true);

    private bool StaticKeywordSuppressed(CardInstance card, string keyword)
    {
        if (keyword == "Brash" && Definition(card).Effect == LosesBrashVsLionEffect && Player(1 - card.OwnerId).Clan == "Lion") return true;
        return AimiSuppressedKeywords.Contains(keyword, StringComparer.Ordinal) && AskStaticKeywordRules(card, keyword, granted: false);
    }

    private bool AskStaticKeywordRules(CardInstance card, string keyword, bool granted)
    {
        var key = (card.Id, keyword);
        if (!_staticKeywordQuestions.Add(key)) return false;
        try
        {
            foreach (var source in StaticKeywordRuleSources(card.OwnerId))
            {
                switch (Definition(source).Effect)
                {
                    case AimiLionCourtierEffect:
                        if (InPlay(card) && Definition(card).Type == CardType.Personality && HasClanAffiliation(card, "Lion")
                            && (granted ? keyword == "Courtier" : AimiSuppressedKeywords.Contains(keyword, StringComparer.Ordinal))) return true;
                        break;
                    case NagiMagistrateClanEffect:
                        if (granted && keyword.EndsWith(" Clan", StringComparison.Ordinal) && ControllerClans(card.OwnerId).Contains(keyword[..^" Clan".Length], StringComparer.Ordinal)
                            && NagiCovers(source, card)) return true;
                        break;
                    case NobukatsuMantisMagistrateEffect:
                        if (granted && keyword == "Magistrate" && Keyword(card, "Samurai") && Keyword(card, "Mantis Clan")) return true;
                        break;
                }
            }
            return false;
        }
        finally { _staticKeywordQuestions.Remove(key); }
    }

    // Zansho: "Your Followers have +1F or a Force bonus equal to their Honor Requirement, whichever is greater." A static Force term of FollowerForce, read live for every
    // Follower in play of Zansho's controller (a Follower in play is attached to a Personality), per Zansho in play.
    private const string ZanshoFollowerForceEffect = "zansho-follower-force";
    private int ZanshoFollowerBoost(CardInstance follower)
    {
        if (follower.Zone != Zone.Attached || Definition(follower).Type != CardType.Follower) return 0;
        var senseis = _state.Cards.Values.Count(c => c.OwnerId == follower.OwnerId && InPlay(c) && Definition(c).Effect == ZanshoFollowerForceEffect);
        return senseis == 0 ? 0 : senseis * Math.Max(1, Definition(follower).HonorRequirement ?? 0);
    }

    // Mukudori: "Your other Fudo cards enter play for 1 less Gold." A term of every entry price (QuotePurchase, EquipGoldCost): 1 per Mukudori in play of the recruiting or
    // equipping player's, for a card with Fudo other than that Mukudori itself.
    private const string MukudoriFudoDiscountEffect = "mukudori-fudo-discount";
    private int KeywordEntryDiscount(CardInstance entering, int playerId) =>
        Keyword(entering, "Fudo") ? _state.Cards.Values.Count(c => c.Id != entering.Id && c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == MukudoriFudoDiscountEffect) : 0;

    // The Sensei entry trait ("After X enters play: Discard all your other Sensei Personalities") on a card whose Effect field is taken by its OTHER static trait: the five
    // Seeds of Decay Sensei. discard-other-sensei itself stays the Effect of a card with no other trait.
    private static bool HasSenseiEntryEffect(string effect) => effect is ZanshoFollowerForceEffect or MukudoriFudoDiscountEffect or AimiLionCourtierEffect or NagiMagistrateClanEffect or NobukatsuMantisMagistrateEffect;
}
