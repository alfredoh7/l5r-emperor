using L5R.Core;

namespace L5R.Client;

/// <summary>Player-facing explanations, separate from presentation and authoritative rules.
/// Stable topic keys can also be used by a future scripted tutorial.</summary>
public static class RulesHelp
{
    public static readonly (string Key, string Title, string Body)[] Topics =
    [
        ("bow", "Bowed / unbowed", "A sideways card is bowed: it has been used. Upright cards are unbowed. Cards normally straighten at the start of their controller's turn; some effects prevent this. Each attachment keeps its own bowed state."),
        ("gold", "Gold & payment", "Ready Gold is potential production, not a stored balance. Choose unbowed Holdings or your Stronghold to pay a cost. Those sources bow when you confirm. Excess production is lost after that payment."),
        ("province", "Provinces & home", "Each province holds a Dynasty card until it enters play. Recruited Personalities and Holdings live at home; they do not belong to their original province. During an attack, each unit is assigned to a defending province's battlefield."),
        ("unit", "Personalities & attachments", "A unit is a Personality and its attached cards. Items and Followers belong to that Personality, not a province. They move together. Use the + number beneath a unit to spread its attachments and inspect each card."),
        ("force", "Force, Chi & Personal Honor", "Force (F) determines army strength in battle. A bowed ordinary Personality prevents its entire unit from contributing at resolution. Reducing an unbowed Personality to 0 Force does not remove its Followers’ contribution. Chi (C) and Personal Honor are separate statistics used by card effects. Current values in the card’s corner panels include active modifiers; the spear standard shows the unit’s battle contribution."),
        ("battle", "Battle resolution", "Players alternate Battle actions. When both pass, the stronger army destroys the weaker army. A tie destroys both armies if both contain units. To destroy the province, attacking Force must exceed defending Force plus province strength (PS)."),
        ("timing", "Limited, Open & Battle", "Limited actions are taken during your own Action Phase. Open actions can be taken in either player's Action Phase when you have priority. Battle actions are taken in a battle and must meet their printed requirements. Disabled actions explain why they cannot be used now."),
        ("honor", "Family Honor & Proclaim", "Family Honor belongs to your Clan; Personal Honor belongs to an individual Personality. Recruiting an eligible Clan Personality without the Clan discount can open a Proclaim reaction to gain its Personal Honor. The purchase preview tells you whether this is available."),
        ("favor", "Imperial Favor", "Lobby during your Action Phase by bowing an eligible Personality while you have higher Family Honor. The Favor can then be discarded for its Limited card exchange or Battle action. Select the relevant action to see legal performers and targets."),
        ("targets", "Targets & confirmation", "Gold outlines mark legal choices; blue outlines mark your selections. Arrows show intent. Select a target or payment sources, review the action, then confirm. Cancel clears the plan without changing the match.")
    ];

    public static string Topic(string key) => Topics.First(t => t.Key == key).Body;
    public static string PhaseGuide(Phase phase) => phase switch
    {
        Phase.Straighten => "Resolve straightening of eligible cards. Effects may prevent a card from straightening or offer a choice to leave it bowed; follow any required choice shown by the game.",
        Phase.Events => "Resolve Events-phase effects and any required choices. When none require a decision, the game continues automatically.",
        Phase.Action => "Use a Limited or Open action, or attach a card from your hand. Select a card to see its legal actions. Pass when you are ready to move on.",
        Phase.AttackDeclaration => "Choose a highlighted Personality, then an opposing province. Repeat for each attacker and confirm. Leave units at home to keep them out of this attack.",
        Phase.DefenseAssignment => "Choose your defenders and the provinces they will protect. A Personality and its attachments move together when you confirm.",
        Phase.BattleSelection => "Choose a contested province to resolve next. Each battlefield resolves separately; its units are shown in the same lane.",
        Phase.Battle => "Select a card to use a Battle action, or pass. Two consecutive passes resolve this battle. Check both armies' Force and the province strength first.",
        Phase.Dynasty => "Recruit a face-up Dynasty card from a province. Review its cost, choose unbowed Gold sources and confirm. You may recruit again while you can pay.",
        Phase.DynastyDiscard => "Discard unwanted face-up Dynasty cards from your provinces, or finish the phase to keep them. Replacements enter face-down.",
        Phase.End => "Resolve any remaining choices. The game handles the end of the turn and prepares the next player automatically.",
        _ => "The match has ended. Start a new practice from the menu."
    };

    public static string Keyword(string keyword) => keyword switch
    {
        "Armor" or "Weapon" => $"{keyword}: A Personality can have only one attachment with this keyword. The attachment modifies its Personality's printed statistics.",
        "Unique" => "Unique: You cannot bring another copy of this Unique card into play while you already control one.",
        "Shugenja" => "Shugenja: A Personality trait required by certain actions, such as Shinden Asahina. Select a card to see which actions it can perform.",
        "Courtier" => "Courtier: A Personality trait used by political effects and their targeting requirements.",
        "Samurai" => "Samurai: A Personality trait. Card effects can require or target a Samurai; the word itself does not add Force.",
        _ => keyword + ": A printed trait used to identify this card for effects and their requirements. Available actions are listed separately."
    };

    public static string ActionGuide(string type) => type switch
    {
        "purchase" => Topic("gold"),
        "equip" => "Attach this card to a legal Personality. Choose the recipient and any Gold sources, then confirm. " + Topic("unit"),
        "attack" or "assignDefense" => Topic("targets"),
        "pass" => "Give the other player an opportunity to act. Two consecutive passes usually advance the current action window; in battle they resolve combat.",
        "endDynasty" => "End your Dynasty Phase and turn. Discard unwanted province cards before ending if you wish; starting to discard prevents further recruiting.",
        "proclaim" => Topic("honor"),
        "selectBattle" => PhaseGuide(Phase.BattleSelection),
        _ => Topic("timing")
    };

    public static string Tooltip(string text, int columns = 62)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length + word.Length + 1 > columns) { lines.Add(line); line = ""; }
                line += (line.Length == 0 ? "" : " ") + word;
            }
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }
}
