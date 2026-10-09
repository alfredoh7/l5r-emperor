using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>One distinct action a selected card can legally take now. Variants differ only by performer, target or alternative cost.</summary>
internal sealed record PlayChoice(string Key, ActionOption Option, ActionOption[] Variants);

public partial class Main
{
    // The card and action the player is being asked to confirm. Held as a key, not an option, so a refreshed snapshot re-resolves it.
    private (string CardId, string Key)? _playPrompt;

    private static string PlayKey(ActionOption option) => option.Type + ":" + (option.Command?.AbilityId ?? option.AlternativeCost?.AbilityId ?? "");

    private bool IsOwnHandCard(string? id) => id is not null && FindCard(id) is { Zone: Zone.Hand } card && card.OwnerId == View.ViewerId;

    /// <summary>The distinct legal actions of a card. The engine already disables an action that is out of phase or lacks its performer, so an enabled option is a legal one.</summary>
    private PlayChoice[] PlayChoices(CardView card) => Actions
        .Where(a => a.Enabled && a.CardId == card.Id && a.Command is not null && a.Type is "equip" or "playStrategy" or "useAbility")
        .GroupBy(PlayKey).Select(g => new PlayChoice(g.Key, g.First(), g.ToArray())).ToArray();

    private static bool IsLimited(PlayChoice choice) =>
        choice.Option.Command?.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing.Contains("Limited", StringComparison.Ordinal);

    /// <summary>The action to assume: the only legal one, or, while Plans Within Plans is armed, the Limited action it permits. Otherwise the player picks.</summary>
    private PlayChoice? AssumedPlay(PlayChoice[] choices)
    {
        if (choices.Length == 1) return choices[0];
        if (View.Players.Single(p => p.Id == View.ViewerId).PlansWithinPlansArmed && choices.Count(IsLimited) == 1) return choices.Single(IsLimited);
        return null;
    }

    /// <summary>Selecting a playable hand card starts the play flow without a trip through the sidebar's action list.</summary>
    private void BeginDirectPlay(CardView card)
    {
        _playPrompt = null;
        if (!IsOwnHandCard(card.Id) || card.ActionLocked || View.PendingChoice is not null || View.WinnerId is not null || View.PriorityPlayerId != View.ViewerId || _chosen is not null) return;
        if (AssumedPlay(PlayChoices(card)) is { } choice) _playPrompt = (card.Id, choice.Key);
    }

    private void BeginPlayPrompt(ActionOption option)
    {
        if (option.CardId is null || _session.Busy || PresentationBusy) return;
        _playPrompt = (option.CardId, PlayKey(option));
        RenderInspector(); RenderPhaseActions(); Highlight();
    }

    private PlayChoice? PromptedChoice() =>
        _playPrompt is { } prompt && FindCard(prompt.CardId) is { } card && IsOwnHandCard(card.Id) ? PlayChoices(card).FirstOrDefault(c => c.Key == prompt.Key) : null;

    /// <summary>True when playing the action needs a performer, target, province, movement, cost payment or alternative cost. Otherwise confirming plays it outright.</summary>
    private bool NeedsFurtherInput(PlayChoice choice)
    {
        var option = choice.Option;
        return NeedsTargetPick(option) || option.TargetIds is { Length: > 0 } || option.Slots is not null || IsProvinceTarget(option) || option.Movement is not null
            || HasAttackTargets(option) || option.Attack is not null || option.AlternativeCost is not null || option.Gold is { Cost: > 0 } || option.Gold is { Kind: GoldCostKind.Variable }
            || option.TargetGold is not null || choice.Variants.Select(v => v.Command?.PerformerId).Distinct().Count() > 1 || option.Command?.CardIds is not null;
    }

    private bool RenderPlayPrompt()
    {
        if (PromptedChoice() is not { } choice || FindCard(_playPrompt!.Value.CardId) is not { } card) { _playPrompt = null; return false; }
        var others = PlayChoices(card).Where(c => c.Key != choice.Key).ToArray();
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); panel.AddChild(box); _inspector.AddChild(panel);
        box.AddChild(Palette.Text("PLAY THIS CARD?", 12, Palette.Gold));
        box.AddChild(Palette.Wrapped(card.Definition?.Name ?? "Card", 22));
        box.AddChild(Palette.Wrapped(Placard(choice.Option), 17, Palette.Gold));
        box.AddChild(Palette.Wrapped(NeedsFurtherInput(choice)
            ? "Confirm to begin. You then choose any performer, target and payment."
            : "Nothing else to choose. Confirm to play it.", 14, Palette.PaperDim));
        foreach (var other in others)
        {
            var option = other.Option;
            var switchTo = SidebarSkin.Button("Play instead: " + Placard(option), () => BeginPlayPrompt(option));
            switchTo.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.AddChild(switchTo);
        }
        var confirm = SidebarSkin.Button("Confirm play", ConfirmPlay, true); confirm.Disabled = _session.Busy;
        _decisionActions.AddChild(confirm);
        _decisionActions.AddChild(SidebarSkin.Button("Cancel", CancelChoice));
        return true;
    }

    private void ConfirmPlay()
    {
        if (PromptedChoice() is not { } choice) { _playPrompt = null; RenderInspector(); return; }
        _playPrompt = null;
        if (!NeedsFurtherInput(choice) && choice.Option.Command is { } command) { _ = Submit(command); return; }
        Choose(choice.Option);
    }

    /// <summary>A card with several legal actions lists each as a highlightable, clickable block under its printed text; a click begins that action.</summary>
    private void AddActionTextBlocks(VBoxContainer details, CardView card)
    {
        if (_chosen is not null || _playPrompt is not null || View.PendingChoice is not null || !IsOwnHandCard(card.Id)) return;
        var choices = PlayChoices(card);
        if (choices.Length < 2) return;
        details.AddChild(Palette.Text("CHOOSE AN ACTION", 11, Palette.Gold));
        foreach (var choice in choices)
        {
            var option = choice.Option;
            var block = new Button
            {
                Text = Placard(option),
                Flat = false,
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0, 40),
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                TooltipText = "Click to play this action. You confirm before anything happens."
            };
            block.SetMeta("action_text_block", true);
            block.AddThemeFontSizeOverride("font_size", 16);
            foreach (var state in new[] { "normal", "pressed", "hover", "focus" })
                block.AddThemeStyleboxOverride(state, new StyleBoxFlat
                {
                    BgColor = state == "hover" ? new Color(Palette.Gold, .16f) : new Color(1, 1, 1, .03f),
                    BorderColor = state == "hover" ? Palette.Gold : new Color(Palette.Gold, .35f),
                    BorderWidthLeft = 2,
                    BorderWidthRight = 1,
                    BorderWidthTop = 1,
                    BorderWidthBottom = 1,
                    ContentMarginLeft = 10,
                    ContentMarginRight = 8,
                    ContentMarginTop = 6,
                    ContentMarginBottom = 6
                });
            block.Pressed += () => BeginPlayPrompt(option);
            details.AddChild(block);
        }
    }
}
