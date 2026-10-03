using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class DuelScreen
{
    private string? _focusChoiceTarget;
    private int _focusChoiceScrollX;
    private bool FocusChoicePending => Snapshot?.View.PendingChoice?.Type == "focusEffectChoice";
    internal string? SelectedFocusChoiceTarget => _focusChoiceTarget;
    private IEnumerable<CardView> FocusChoiceCards()
    {
        if (Snapshot is null) return [];
        return Snapshot.View.Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame)
            .Concat(p.Focused ?? []).Concat(p.Hand).Append(p.Stronghold))
            .Concat(Snapshot.View.LastDuel?.Reveals.LastOrDefault()?.Cards ?? []);
    }
    private CardView? FocusChoiceCard(string? id) => id is null ? null : FocusChoiceCards().FirstOrDefault(c => c.Id == id && c.Definition is not null);
    private void RenderFocusChoice()
    {
        var view = Snapshot!.View; var pending = view.PendingChoice!;
        var options = Legal.Where(a => a.Type == "chooseFocusEffect").ToArray();
        var source = FocusChoiceCard(pending.CardId);
        var canChoose = pending.PlayerId == view.ViewerId && options.Length > 0;
        var decline = options.FirstOrDefault(a => a.Command!.Choice == "decline");
        var choices = options.Where(a => a.Command!.Choice != "decline").ToArray();
        var selected = choices.FirstOrDefault(a => a.Command!.TargetId == _focusChoiceTarget);
        var returns = choices.Any(a => a.Label.StartsWith("Put ", StringComparison.Ordinal) && a.Label.EndsWith(" into your hand", StringComparison.Ordinal));
        var cardChoices = choices.Where(a => FocusChoiceCard(a.Command!.TargetId) is not null).ToArray();
        var effectChoices = choices.Where(a => FocusChoiceCard(a.Command!.TargetId) is null).ToArray();
        var design = new Vector2(1600, 405);
        var origin = new Vector2(36, 526);
        var tray = new Control { Name = "Focus Effect choice tray", Position = _offset + origin * _scale, Size = design, Scale = Vector2.One * _scale }; _hud.AddChild(tray);
        _controlsRect = new Rect2(tray.Position, design * _scale);
        DuelDecisionArt.Background(tray, true);
        Label Words(string text, Vector2 at, Vector2 size, int font = 20, bool title = false, Color? color = null)
        {
            var label = DuelUi.Text(text, font, color ?? Palette.Paper, title); label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.Position = at; label.Size = size; label.MouseFilter = MouseFilterEnum.Ignore; tray.AddChild(label); return label;
        }
        var prompt = pending.Prompt;
        var prefix = $"{source?.Definition?.Name} (Focus Effect): {view.Players[pending.PlayerId].Name}, ";
        if (prompt.StartsWith(prefix, StringComparison.Ordinal)) prompt = prompt[prefix.Length..];
        if (prompt.Length > 0) prompt = char.ToUpperInvariant(prompt[0]) + prompt[1..];
        Words(canChoose ? "YOUR CHOICE" : "OPPONENT’S CHOICE", new(42, 30), new(415, 54), canChoose ? 42 : 33, true);
        Words(canChoose ? (returns ? "Choose a card to return to your hand." : effectChoices.Length == choices.Length ? "Choose an effect." : "Choose a target.") : $"Waiting for {view.Players[pending.PlayerId].Name}.", new(44, 87), new(400, 58), 23);
        Words(canChoose ? (returns ? "From your discard pile · " : "") + (decline is null ? "Required choice" : "Optional choice") : "The duel continues after their choice.", new(44, 144), new(400, 30), 17, color: Palette.PaperDim);
        if (source is not null)
        {
            AddFocusChoiceFace(tray, source, new Vector2(48, 189), 168, () => Inspect(source));
            Words(source.Definition!.Name, new(185, 192), new(246, 63), 24);
            Words("Focus Effect", new(185, 251), new(246, 26), 18, color: Palette.Gold);
            var inspect = DuelUi.Button("Inspect source", () => Inspect(source), fontSize: 17); inspect.Position = new(185, 292); inspect.Size = new(230, 38); tray.AddChild(inspect);
        }
        var divider = new ColorRect { Position = new(464, 42), Size = new(1, 313), Color = new Color(.6f, .47f, .25f, .7f), MouseFilter = MouseFilterEnum.Ignore }; tray.AddChild(divider);
        if (!canChoose)
        {
            Words(prompt, new(515, 115), new(940, 100), 26);
            Words("Their choices remain on their screen.", new(515, 253), new(940, 55), 21, color: Palette.PaperDim);
            return;
        }
        Words(cardChoices.Length > 0 ? "ELIGIBLE CARDS" : "AVAILABLE EFFECTS", new(502, 27), new(600, 35), 22);
        var scroll = new ScrollContainer { Name = "Focus choices", Position = new(493, 67), Size = new(615, 312), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled }; tray.AddChild(scroll);
        var retainedScroll = _focusChoiceScrollX;
        scroll.GetHScrollBar().ValueChanged += value => _focusChoiceScrollX = (int)value;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 26); scroll.AddChild(row);
        // Leave room for the horizontal scrollbar when the legal set exceeds one row.
        var crowdedOffset = cardChoices.Length > 2 ? 16 : 0;
        foreach (var option in choices)
        {
            var card = FocusChoiceCard(option.Command!.TargetId);
            var column = new Control { CustomMinimumSize = new Vector2(card is null ? 276 : 210, 285) }; row.AddChild(column);
            if (card is null)
            {
                var effect = DuelUi.Button(option.Label, () => Submit(option.Command!), fontSize: 29);
                effect.AutowrapMode = TextServer.AutowrapMode.WordSmart; effect.Position = new(0, 42); effect.Size = new(276, 178); column.AddChild(effect);
                effect.Disabled = InputLocked || _submitted || _commandCooldown > 0;
                continue;
            }
            var isSelected = option.Command.TargetId == _focusChoiceTarget;
            void Pick() { if (InputLocked || _submitted) return; _focusChoiceTarget = option.Command.TargetId; RenderActions(); }
            var face = AddFocusChoiceFace(column, card, new Vector2(14, 3), 247 - crowdedOffset, Pick);
            face.Name = "Choice " + card.Id;
            if (isSelected)
            {
                var outline = new Panel { Position = new(12, 1), Size = new(185 - crowdedOffset * .72f, 251 - crowdedOffset), MouseFilter = MouseFilterEnum.Ignore };
                outline.AddThemeStyleboxOverride("panel", Palette.Box(Colors.Transparent, Palette.Gold, 2, 0)); column.AddChild(outline);
            }
            var pick = FocusChoiceTextButton(isSelected ? "Selected" : $"Select · {view.Players[card.OwnerId].Name}", Pick, 16);
            pick.Position = new(0, 255 - crowdedOffset); pick.Size = new(210, 25); column.AddChild(pick);
            var inspect = FocusChoiceTextButton("Inspect", () => Inspect(card), 15); inspect.Position = new(47, 285 - crowdedOffset); inspect.Size = new(116, 25);
            // Scroll content includes a separate inspection affordance; selecting never submits.
            column.CustomMinimumSize = new(210, 308 - crowdedOffset); column.AddChild(inspect);
        }
        var rightX = 1150f;
        Words(returns ? "YOUR HAND" : "RESOLVE EFFECT", new(rightX, 47), new(392, 36), 24);
        Words(selected is null ? (cardChoices.Length > 0 ? (returns ? "Select a card to see the action." : prompt) : prompt)
            : selected.Label, new(rightX, 98), new(382, 91), 23);
        if (selected is not null)
        {
            // The cue is part of the selected action, not a line falsely originating at a different card.
            Words(returns ? "DISCARD → HAND" : "SELECTED TARGET", new(rightX, 201), new(382, 30), 18, color: Palette.Gold);
        }
        if (cardChoices.Length > 0)
        {
            var confirm = DuelUi.Button(selected is null ? "Select a card" : returns ? "Return to hand" : selected.Label, () => { if (selected?.Command is { } command) Submit(command); }, fontSize: 25);
            confirm.Name = "Resolve Focus choice"; confirm.AutowrapMode = TextServer.AutowrapMode.WordSmart; confirm.Position = new(rightX, 239); confirm.Size = new(380, 64); tray.AddChild(confirm);
            confirm.Disabled = selected is null || InputLocked || _submitted || _commandCooldown > 0;
        }
        if (decline?.Command is { } declineCommand)
        {
            var skip = DuelUi.Button(returns ? "Take no card" : "Decline effect", () => Submit(declineCommand), fontSize: 21);
            skip.Position = new(rightX, 310); skip.Size = new(380, 42); tray.AddChild(skip);
            skip.Disabled = InputLocked || _submitted || _commandCooldown > 0;
        }
        Words("The duel continues after your choice.", new(rightX, 361), new(395, 26), 16, color: Palette.PaperDim);
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, retainedScroll);
        // A complete engine prompt remains available without crowding the primary choice.
        tray.TooltipText = prompt;
    }
    private static Button FocusChoiceTextButton(string text, Action action, int fontSize)
    {
        var button = DuelUi.Button(text, action, fontSize: fontSize); button.CustomMinimumSize = Vector2.Zero;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginTop = 2, ContentMarginBottom = 2, ContentMarginLeft = 4, ContentMarginRight = 4 });
        button.AddThemeColorOverride("font_hover_color", Palette.Gold);
        return button;
    }
    private CardTile AddFocusChoiceFace(Control parent, CardView card, Vector2 position, float height, Action choose)
    {
        var face = new CardTile(card with { Bowed = false }, true, _ => choose(), _ => { })
        { Size = Vector2.One * (height + 12), FaceHeight = height, ShowTokenRail = false, ReducedMotion = true, Material = CardPaper.Reading };
        // CardTile uses a square control with the full portrait centered inside it.
        face.Position = position - new Vector2((height + 12 - height * .72f) / 2, 0); parent.AddChild(face); return face;
    }
}
