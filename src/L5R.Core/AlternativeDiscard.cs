namespace L5R.Core;

// A discarded hand card as one way to pay an ability's cost (Akodo Kamina, issue #198 follow-up): "Bow: Give a target Personality +4F. You may discard a card to ignore the cost of bowing Kamina."
// It is an alternative cost (AlternativeCosts.cs): CostOptions lists "Bow Kamina" and AlternativeDiscard.Option, the acting player picks one, and the discard names its hand card on
// GameCommand.CardIds (CostSubject.Chosen). QueryActions lists the discard option once per card in the hand, so a client shows one choice per card; with an empty hand it is listed
// disabled with the reason. Paying with the discard leaves Kamina unbowed.
public static class AlternativeDiscard
{
    public const string OptionId = "discard";
    /// <summary>"Discard a card": the hand card the command names.</summary>
    public static CostOption Option { get; } = new(OptionId, "Discard a card", CostVerbs.Discard, CostSubject.Chosen);

    internal static bool IsDiscardOption(CostOption? option) => option is { Verb: CostVerbs.Discard, Subject: CostSubject.Chosen };
    /// <summary>Whether the ability offers a discard cost option, so its command may carry the chosen card on CardIds.</summary>
    internal static bool CarriesCostCard(string abilityId) =>
        AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.CostOptions?.Any(IsDiscardOption) == true;

    /// <summary>The cards an offered action may name for this cost option: each card of the hand for a discard option, otherwise the ability's own choices.</summary>
    internal static string?[] Choices(CostOption? option, string?[] otherwise, PlayerState player, CardInstance? source) =>
        IsDiscardOption(option) ? player.Hand.Where(h => h != source?.Id).Select(h => (string?)h).DefaultIfEmpty(null).ToArray() : otherwise;
}

public sealed partial class MatchEngine
{
    // The chosen hand card, or the reason the command does not name a valid one.
    private void ValidateDiscardOption(AbilityDefinition ability, CostOption option, GameCommand command, CardInstance? source)
    {
        var player = Player(command.PlayerId);
        Require(command.CardIds is [var chosenId] && player.Hand.Contains(chosenId) && chosenId != source?.Id && _state.Cards.ContainsKey(chosenId),
            player.Hand.Any(h => h != source?.Id) ? "Choose one card from your hand to discard." : "You have no card in your hand to discard.");
    }

    // The discard is paid with the rest of the cost (ExecuteAbilityEffects' step right after the Gold); the card has already been validated and is still checked in case
    // a reaction between announcement and resolution took it out of the hand.
    private void PayDiscardOption(AbilityDefinition ability, CostOption option, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (command.CardIds is not [var chosenId] || !player.Hand.Contains(chosenId)) return;
        DiscardFate(player, Card(chosenId));
    }
}
