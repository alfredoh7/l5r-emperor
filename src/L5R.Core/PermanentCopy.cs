namespace L5R.Core;

// Copying a whole card's rules, permanently (issue #420, Mendon's ruling on Mastering the Elements): "This card permanently copies the Spell's keywords, traits, and its base abilities that do not
// themselves copy abilities." Where CardCopy.cs copies ONE chosen keyword or ability for a while (Fukuzo), this takes everything but the title, for good.
//
//   * it is written into the copier's own printed values (CardInstance.Printed, the per-instance override that Definition(card) reads everywhere): the keywords are added, the abilities are added, and
//     the Spell's trait -- the Effect string that drives its static bonus -- is taken. Because the copy IS the card's printed text from then on, it is permanent for the game, it survives leaving and
//     re-entering play, and nothing that clears a card's grants or modifiers (which is everything that resets it on leaving play) can undo it;
//   * "base abilities" are the abilities the Spell prints, not ones it was granted or copied; an ability that copies abilities (Mastering the Elements' own, Fukuzo's) is never taken, and neither is
//     one that triggers on its own card entering play, which has already happened for the copier;
//   * the once-per-game Reaction comes back each time the card enters play (the entry window clears its stamp, as it does for every own-enters-play Reaction), and a later copy ADDS to what an earlier
//     one gave: keywords and abilities accumulate. A card has one trait slot, so a later Spell's trait replaces an earlier one's (readings for Mendon in the decision record);
//   * the Gold paid is the Spell's base Gold Cost: the ability's own cost rule (GoldCostRule.TargetPrinted).
public sealed partial class MatchEngine
{
    private void PermanentlyCopySpell(CardInstance copier, CardInstance spell)
    {
        var printed = Definition(copier);
        var keywords = (printed.Keywords ?? []).ToList();
        foreach (var keyword in CurrentKeywordList(spell)) if (!keywords.Contains(keyword, StringComparer.Ordinal) && !KeywordGainBlocked(copier, keyword)) keywords.Add(keyword);
        var abilities = (printed.Abilities ?? []).ToList();
        var copiedAbilities = new List<string>();
        foreach (var id in Definition(spell).Abilities ?? [])
            if (AbilityCatalog.All.TryGetValue(id, out var ability) && CopyableAbility(ability) && !abilities.Contains(id, StringComparer.Ordinal)) { abilities.Add(id); copiedAbilities.Add(id); }
        var spellTrait = Definition(spell).Effect;
        var effect = spellTrait != "none" ? spellTrait : printed.Effect;
        var newKeywords = keywords.Except(printed.Keywords ?? [], StringComparer.Ordinal).ToArray();
        copier.Printed = printed with { Keywords = keywords.ToArray(), Abilities = abilities.ToArray(), Effect = effect };
        Emit("spellCopied", $"{Definition(copier).Name} permanently copies {Definition(spell).Name}: {(newKeywords.Length == 0 ? "no new keywords" : string.Join(", ", newKeywords))}"
            + $"{(spellTrait != "none" ? ", its trait" : "")}{(copiedAbilities.Count == 0 ? "" : $", and {copiedAbilities.Count} abilit{(copiedAbilities.Count == 1 ? "y" : "ies")}")}.", copier.OwnerId, copier.Id);
    }

    // Sly Deceiver (Before the Dawn, issue #431): "Your Ninja permanently copies the Personality's base keywords, except Clan Champion, Daimyo, or Shugenja, and permanently copies his base abilities
    // which do not themselves copy abilities." The same permanent copy as Mastering the Elements' (its Spell's keywords, trait and abilities), read from a Personality's printed values.
    private void PermanentlyCopyPersonality(CardInstance copier, CardInstance from)
    {
        var printed = Definition(copier);
        string[] excluded = ["Clan Champion", "Daimyo", "Shugenja"];
        var keywords = (printed.Keywords ?? []).ToList();
        var newKeywords = new List<string>();
        foreach (var keyword in Definition(from).Keywords ?? [])
            if (!excluded.Contains(keyword, StringComparer.Ordinal) && !keywords.Contains(keyword, StringComparer.Ordinal) && !KeywordGainBlocked(copier, keyword)) { keywords.Add(keyword); newKeywords.Add(keyword); }
        var abilities = (printed.Abilities ?? []).ToList();
        var copied = 0;
        foreach (var id in Definition(from).Abilities ?? [])
            if (AbilityCatalog.All.TryGetValue(id, out var ability) && CopyableAbility(ability) && !abilities.Contains(id, StringComparer.Ordinal)) { abilities.Add(id); copied++; }
        copier.Printed = printed with { Keywords = keywords.ToArray(), Abilities = abilities.ToArray() };
        Emit("personalityCopied", $"{Definition(copier).Name} permanently copies {Definition(from).Name}: {(newKeywords.Count == 0 ? "no new keywords" : string.Join(", ", newKeywords))}, and {copied} abilit{(copied == 1 ? "y" : "ies")}.", copier.OwnerId, copier.Id);
    }
}
