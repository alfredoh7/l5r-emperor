using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private void RenderInspector()
    {
        Palette.Clear(_sidebarHeader); Palette.Clear(_inspector); Palette.Clear(_decisionActions); _paymentConfirm = null;
        _guide.Text = View.PendingChoice?.Prompt ?? (_chosen is { Type: "attack" or "assignDefense" } ? "Select a unit, then its destination province. Review the arrows and confirm assignments." : _chosen is { Type: "purchase" or "recruitReserve" } ? "Choose ready Gold sources. Confirm pays the cost and brings the card into play." : _chosen is not null && QuoteFor(_chosen) is not null ? "Review the cost and its basis, then choose ready Gold sources. Confirm pays and resolves; cancel changes your plan." : _chosen is not null ? "Review the highlighted target. Confirm to resolve, or cancel to change your plan." : View.PriorityPlayerId != View.ViewerId ? "Your opponent is deciding. You can hover over visible cards to read them while you wait." : RulesHelp.PhaseGuide(View.Phase));
        _guide.Visible = View.PendingChoice is not null || _chosen is not null;
        _guide.TooltipText = RulesHelp.Tooltip(RulesHelp.PhaseGuide(View.Phase) + "\n\n" + RulesHelp.Topic("targets"));
        if (View.WinnerId is null && View.PendingDuel?.Stage is ("AwaitingAccept" or "Reaction") && View.LastDuel is { } duel)
        {
            _guide.Hide();
            _inspector.AddChild(Palette.Text("CHALLENGE", 25, title: true));
            _inspector.AddChild(Palette.Wrapped($"{duel.Challenger.Definition!.Name} challenges {duel.Challenged.Definition!.Name}", 22));
            if (duel.Challenge is { } stakes) { _inspector.AddChild(Palette.Wrapped(stakes.AbilityLabel, 19, Palette.Gold)); _inspector.AddChild(Palette.Wrapped(stakes.LossTerms, 17)); if (stakes.Refusable) _inspector.AddChild(Palette.Wrapped(stakes.RefusalTerms, 17, Palette.PaperDim)); }
            if (View.PendingChoice is { } reaction) _inspector.AddChild(Palette.Wrapped(reaction.Prompt, 18));
            foreach (var option in Actions.Where(a => a.Enabled && a.Command is not null && a.Type is not ("concede" or "declineReaction")))
            { var command = option.Command!; _inspector.AddChild(SidebarSkin.Button(option.Label, () => _ = Submit(command))); }
            return;
        }
        if (RenderBattleFlow()) return;
        if (PresentationBusy)
        {
            _guide.Visible = false;
            _sidebarHeader.AddChild(new BattleTracker
            {
                Snapshot = View,
                ReducedMotion = _reducedMotion,
                ResolutionMessage = _battleOutcomeText,
                PresentationInProgress = true,
                ActionOutcome = _actionOutcome
            });
            return;
        }
        if (View.WinnerId is { } winner)
        {
            _inspector.AddChild(Palette.Text("MATCH COMPLETE", 12, Palette.Gold));
            _inspector.AddChild(Palette.Text(View.Players[winner].Clan + " prevails", 28, title: true));
            _inspector.AddChild(Palette.Wrapped(View.VictoryReason ?? "Victory"));
            _inspector.AddChild(SidebarSkin.Button("Start another practice", ShowPractice, true)); return;
        }
        if (BattleTracker.InBattle(View.Phase) || _resolutionMessage is not null)
            _sidebarHeader.AddChild(new BattleTracker { Snapshot = View, Compact = _selected is not null || _chosen is not null || View.PendingChoice is not null, ReducedMotion = _reducedMotion, ResolutionMessage = _resolutionMessage });
        if (!BattleTracker.InBattle(View.Phase) && _resolutionMessage is null)
        {
            var heading = Palette.Text(HumanPhase(View.Phase), 25, title: true); heading.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(heading);
            var status = Palette.Wrapped(View.PriorityPlayerId == View.ViewerId ? "Your decision" : "Opponent may act", 18); status.HorizontalAlignment = HorizontalAlignment.Center; _sidebarHeader.AddChild(status);
            _sidebarHeader.AddChild(new HSeparator());
        }
        if (View.PendingChoice is { } pending)
        {
            _inspector.AddChild(Palette.Text(pending.Type == "discardHand" ? "HAND LIMIT" : pending.Type == "shinden" ? "YOUR CHOICE" : "REACTION WINDOW", 12, Palette.Gold));
            _inspector.AddChild(Palette.Wrapped(pending.Prompt, 20));
            if (pending.Type == "proclaim") _inspector.AddChild(Palette.Wrapped("Honor updates when Proclaim resolves. Each player then gets an opportunity to pass.", 14, Palette.PaperDim));
        }
        if (RenderHomeReminderInspection()) { }
        else if (_selected?.Definition is { } def)
        {
            var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
            var details = new VBoxContainer(); details.AddThemeConstantOverride("separation", 12); panel.AddChild(details); _inspector.AddChild(panel);
            AddInspectionCaption("Card details", details);
            var name = Palette.Wrapped(def.Name, 24); name.AddThemeFontOverride("font", SidebarSkin.Bold); details.AddChild(name);
            if (CardChanges.Entries(_selected).Length > 0)
                details.AddChild(CardChangePanel.Create(_selected, id => FindCard(id)?.Definition?.Name, compact: true,
                    inspectSource: id => { if (FindCard(id) is { } source) SelectCard(source); }));
            var artwork = CardTile.Artwork(def);
            if (artwork is not null)
            {
                var preview = CardPreview.Face(_selected, def.ScenarioOnly ? 190 : 360);
                details.AddChild(preview);
            }
            if (_selected.Dishonorable) details.AddChild(Palette.Wrapped("Dishonorable", 21, new Color("efaa9a")));
            foreach (var grant in _selected.GrantedAbilities ?? [])
                details.AddChild(Palette.Wrapped(GrantedAbilityText.Describe(grant), 16, Palette.Gold));
            if (_selected.StraightenBlocked) details.AddChild(Palette.Wrapped("Straightening is prevented for this turn.", 13, Palette.Gold));
            if (!string.IsNullOrWhiteSpace(def.Text)) details.AddChild(Palette.Wrapped(def.Text, 16, Palette.PaperDim));
            if (def.Type == CardType.Personality && (_selected.Force != def.Force || _selected.Chi != def.Chi)) details.AddChild(Palette.Wrapped($"Current: {_selected.Force} Force · {_selected.Chi} Chi", 16, Palette.Gold));
            if (_selected.AttachedTo is { } parent) details.AddChild(Palette.Wrapped("Attached to " + (FindCard(parent)?.Definition?.Name ?? parent), 13, Palette.Crane));
        }
        else if (_selected is not null)
        {
            AddInspectionCaption("FACE-DOWN DYNASTY");
            _inspector.AddChild(Palette.Wrapped("This card's identity remains hidden until the rules reveal it.", 15, Palette.PaperDim));
        }
        else if (View.PendingChoice is null)
        {

            var help = Palette.Wrapped("Select a card to inspect it.", 16, Palette.PaperDim);
            help.MouseFilter = Control.MouseFilterEnum.Stop; help.TooltipText = RulesHelp.PhaseGuide(View.Phase);
            _inspector.AddChild(help);
        }
        if (_chosen is { Type: "purchase" or "recruitReserve", Quote: not null }) { RenderPurchase(); return; }
        if (_chosen is { Type: "attack" or "assignDefense" }) { RenderAssignments(); return; }
        if (_chosen is { Type: "useAbility", Command.CardIds: not null }) { RenderProvinceRefresh(); return; }
        if (_chosen is { Type: "equip" or "playStrategy" or "useAbility" }) { RenderTargeted(); return; }
        if (_chosen is { Type: "discardHand" }) { RenderHandDiscard(); return; }
        // An inspected reminder must never hide a new engine decision (e.g. hand limit).
        if (_selectedHomeReminder is not null && View.PendingChoice is null) { AddHomeReminderActions(); return; }
        foreach (var action in Actions.Where(a => a.CardId == _selected?.Id && a.CardId is not null && a.Type is not "proclaim")) AddAction(_inspector, action);
        if (View.Phase is Phase.InfantryDeclaration or Phase.DefenseAssignment or Phase.CavalryDeclaration or Phase.CavalryDefense)
        {
            var action = Actions.FirstOrDefault(a => a.Type is "attack" or "assignDefense" && a.Enabled);
            if (action is not null) _inspector.AddChild(SidebarSkin.Button("Assign units…", () => Choose(action), true));
        }
        foreach (var action in Actions.Where(a => a.Type is "selectBattle" or "discardHand" || a.Type == "useAbility" && a.CardId is null)) AddAction(_inspector, action);
    }

    // The recruit review for purchase and recruitReserve: their own mode picker and Proclaim notes (from the
    // engine's PurchaseQuote), then the ONE payment panel every costed action shares.
    private void RenderPurchase()
    {
        var choice = _chosen!;
        var reviewedVersion = choice.Command?.ExpectedVersion ?? View.Version;
        var quote = choice.Quote!;
        var variants = Actions.Where(a => a.Type == choice.Type && a.CardId == choice.CardId && a.Quote is not null).ToArray();
        _inspector.AddChild(Palette.Text(choice.Type == "recruitReserve" ? "RESERVE RECRUIT OPTIONS" : "PURCHASE OPTIONS", 11, Palette.Gold));
        if (variants.Length > 1)
        {
            var modes = new OptionButton { CustomMinimumSize = new Vector2(0, 38), FitToLongestItem = false, ClipText = true };
            foreach (var option in variants)
            {
                var q = option.Quote!;
                var mode = q.ClanDiscount ? q.ClanAllegiance ? "Discount + allegiance" : "Clan discount" : q.ClanAllegiance ? "Clan allegiance" : q.ProclaimEligible ? "Full cost · Proclaim eligible" : "Full cost";
                modes.AddItem($"{q.Cost} Gold · {mode}");
            }
            modes.Selected = Array.IndexOf(variants, choice);
            modes.ItemSelected += index => Choose(variants[(int)index]);
            _inspector.AddChild(modes);
        }
        _inspector.AddChild(Palette.Wrapped(quote.ClanDiscount ? "Clan discount reduces the Gold cost by 2. This purchase cannot trigger Proclaim." : quote.ProclaimEligible ? "Paying without the Clan discount leaves Proclaim available after entry." : "This card is not eligible for Proclaim.", 13, Palette.PaperDim));
        if (quote.ClanAllegiance) _inspector.AddChild(Palette.Wrapped("Clan allegiance adds 2 Gold and ignores the Honor requirement.", 13, Palette.Crane));
        var (required, ready) = RenderGoldPanel(choice);
        AddConfirm(choice.Type == "recruitReserve" ? "Confirm recruit" : "Confirm purchase", required, ready,
            () => new GameCommand(choice.Type, View.ViewerId, reviewedVersion, choice.CardId, required > 0 ? _sources.ToArray() : [], ClanDiscount: quote.ClanDiscount, ClanAllegiance: quote.ClanAllegiance),
            "Cancel payment");
    }

    // The quote the panel shows for the chosen action, straight from the engine: the chosen target's own quote
    // when the cost depends on the target, otherwise the option's single quote. The client never derives a cost.
    private static GoldQuote? QuoteFor(ActionOption action) =>
        action.Command?.TargetId is string target && action.TargetGold is { } byTarget && byTarget.TryGetValue(target, out var targeted) ? targeted : action.Gold;

    // The amount this action pays now: the chosen one for a variable cost (default: the quote's minimum),
    // otherwise the quote's own Cost.
    private static int AmountFor(ActionOption action, GoldQuote quote) =>
        quote.Kind == GoldCostKind.Variable ? Math.Clamp(action.Command?.GoldAmount ?? quote.MinAmount ?? 0, quote.MinAmount ?? 0, quote.MaxAmount ?? quote.MinAmount ?? 0) : quote.Cost;

    // The engine's suggested and alternative payments for the amount being paid (per-amount for a variable cost).
    private static PaymentOption[] PaymentsFor(GoldQuote quote, int amount) =>
        quote.PaymentsByAmount is { } byAmount && byAmount.TryGetValue(amount, out var options) ? options : quote.Payments;

    private static string KindLabel(string kind) => kind switch { GoldCostKind.Variable => "YOU CHOOSE THE AMOUNT", GoldCostKind.Derived => "COST FROM YOUR TARGET", _ => "FIXED COST" };

    // Re-suggest sources from the engine's first payment for the quote now in force (called when the target or
    // the chosen amount changes; the player's own selection is only ever replaced by such a deliberate change).
    private void ResuggestSources()
    {
        _sources.Clear();
        if (_chosen is null || QuoteFor(_chosen) is not { } quote) return;
        foreach (var id in PaymentsFor(quote, AmountFor(_chosen, quote)).FirstOrDefault()?.SourceIds ?? []) _sources.Add(id);
    }

    // Retarget the reviewed action: its command, the quote it is priced by and (when that quote is target-specific)
    // the suggested sources all follow, instantly and without a round trip.
    private void ChooseTarget(string id)
    {
        if (_chosen?.Command is not { } command) return;
        var priced = _chosen.TargetGold?.ContainsKey(id) == true;
        _chosen = _chosen with { Command = command with { TargetId = id } };
        if (priced) ResuggestSources();
        RenderInspector(); Highlight();
    }

    private void ChooseAmount(int amount)
    {
        if (_chosen?.Command is not { } command || QuoteFor(_chosen) is not { Kind: GoldCostKind.Variable } quote) return;
        _chosen = _chosen with { Command = command with { GoldAmount = Math.Clamp(amount, quote.MinAmount ?? 0, quote.MaxAmount ?? quote.MinAmount ?? 0) } };
        ResuggestSources();
        RenderInspector(); Highlight();
    }

    // THE payment panel: what the action costs and why, the amount control for a player-chosen cost, the engine's
    // suggested and alternative payments, the sources to bow and the produced/required line. Every Gold action
    // (recruit, reserve recruit, equip, Strategy, any costed ability) draws it from its GoldQuote and nothing else.
    // Returns the amount to be paid and whether the chosen sources already cover it.
    private (int Required, bool Ready) RenderGoldPanel(ActionOption action)
    {
        if (QuoteFor(action) is not { } quote) return (0, true);
        var required = AmountFor(action, quote);
        _inspector.AddChild(Palette.Text("PAYMENT · " + KindLabel(quote.Kind), 11, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped($"{required} Gold", 24, Palette.Gold));
        if (!string.IsNullOrWhiteSpace(quote.Basis)) _inspector.AddChild(Palette.Wrapped(quote.Basis, 13, Palette.PaperDim));
        if (quote.Error is { } error) _inspector.AddChild(Palette.Wrapped(error, 14, Palette.Error));
        if (quote.Kind == GoldCostKind.Variable) RenderAmountControl(required, quote.MinAmount ?? 0, quote.MaxAmount ?? quote.MinAmount ?? 0);
        var payments = PaymentsFor(quote, required);
        if (payments.Length > 1)
        {
            var suggestions = new OptionButton { CustomMinimumSize = new Vector2(0, 36), FitToLongestItem = false, ClipText = true };
            for (var i = 0; i < payments.Length; i++)
            {
                var p = payments[i];
                suggestions.AddItem($"{(i == 0 ? "Suggested" : "Alternative " + i)} · {p.SourceIds.Length} sources · {p.Excess} excess");
            }
            suggestions.ItemSelected += i => { _sources.Clear(); foreach (var id in payments[(int)i].SourceIds) _sources.Add(id); RenderInspector(); Highlight(); };
            suggestions.Selected = Array.FindIndex(payments, p => p.SourceIds.ToHashSet().SetEquals(_sources));
            _inspector.AddChild(suggestions);
        }
        if (required > 0) RenderSources(required);
        else _inspector.AddChild(Palette.Wrapped("No Gold sources are needed.", 15, Palette.Crane));
        return (required, quote.Error is null && SelectedProduction() >= required);
    }

    private void RenderAmountControl(int amount, int min, int max)
    {
        _inspector.AddChild(Palette.Text("HOW MUCH GOLD", 11, Palette.Gold));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        var less = Palette.Button("−", () => ChooseAmount(amount - 1)); less.Disabled = amount <= min; less.CustomMinimumSize = new Vector2(48, 38);
        var value = Palette.Text($"{amount} Gold  ({min}–{max})", 17); value.HorizontalAlignment = HorizontalAlignment.Center; value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var more = Palette.Button("+", () => ChooseAmount(amount + 1)); more.Disabled = amount >= max; more.CustomMinimumSize = new Vector2(48, 38);
        var most = Palette.Button("Max", () => ChooseAmount(max)); most.Disabled = amount >= max; most.CustomMinimumSize = new Vector2(60, 38);
        row.AddChild(less); row.AddChild(value); row.AddChild(more); row.AddChild(most);
        _inspector.AddChild(row);
    }

    // The confirm/cancel pair every reviewed action ends with. A free action confirms in one click with no
    // sources; a costed one names its cost and waits until the chosen sources cover it.
    private void AddConfirm(string verb, int required, bool ready, Func<GameCommand> build, string cancel, bool enabled = true)
    {
        _paymentConfirm = SidebarSkin.Button(required > 0 ? $"{verb} · {required} Gold" : verb, () => _ = Submit(build()), true);
        _paymentConfirm.Disabled = !enabled || !ready || _session.Busy;
        _decisionActions.AddChild(_paymentConfirm);
        _decisionActions.AddChild(SidebarSkin.Button(cancel, CancelChoice));
    }

    private int SelectedProduction() => Self.Home.Concat(Self.Army).Append(Self.Stronghold).Where(c => _sources.Contains(c.Id) && !c.Bowed).Sum(c => Production(c, Self));

    private void RenderSources(int cost)
    {
        _inspector.AddChild(Palette.Text("SOURCES TO BOW", 11, Palette.Gold));
        foreach (var card in Self.Home.Concat(Self.Army).Append(Self.Stronghold).Where(c => Production(c, Self) > 0))
        {
            var check = new CheckBox { Text = $"{Production(card, Self)} · {ShortName(card)}" + (card.Bowed ? " (bowed)" : ""), ButtonPressed = _sources.Contains(card.Id), Disabled = card.Bowed, CustomMinimumSize = new Vector2(0, 28) };
            check.AddThemeFontSizeOverride("font_size", 14);
            check.Toggled += selected => { if (selected) _sources.Add(card.Id); else _sources.Remove(card.Id); RenderInspector(); Highlight(); };
            _inspector.AddChild(check);
        }
        var produced = SelectedProduction();
        _inspector.AddChild(Palette.Wrapped($"{produced} produced  /  {cost} required" + (produced > cost ? $"\n{produced - cost} excess Gold will be lost." : produced < cost ? "\nChoose more unbowed sources." : "\nExact payment. No excess Gold."), 15, produced < cost ? Palette.Error : Palette.Crane));
    }

    private void RenderAssignments()
    {
        var action = _chosen!;
        var reviewedVersion = action.Command?.ExpectedVersion ?? View.Version;
        _inspector.AddChild(Palette.Text("ASSIGN YOUR UNITS", 12, Palette.Gold));
        if (_assignmentUnit is string selected && FindCard(selected) is { } unit)
            _inspector.AddChild(Palette.Wrapped("Selected: " + ShortName(unit) + "", 16, Palette.Crane));
        foreach (var id in action.TargetIds ?? [])
        {
            var card = FindCard(id)!;
            _inspector.AddChild(Palette.Wrapped($"{ShortName(card)}", 14));
            var provinces = action.Provinces ?? [];
            var picker = new OptionButton(); picker.AddItem("Stay at home");
            foreach (var province in provinces) picker.AddItem(BattlefieldName(province));
            picker.Selected = _assignments.TryGetValue(id, out var assigned) ? Array.IndexOf(provinces, assigned) + 1 : 0;
            picker.ItemSelected += i => { _selected = card; _assignmentUnit = id; if (i == 0) _assignments.Remove(id); else _assignments[id] = provinces[(int)i - 1]; RenderBoard(); RenderInspector(); Highlight(); };
            _inspector.AddChild(picker);
        }
        _decisionActions.AddChild(SidebarSkin.Button("Keep selected unit home", () => { if (_assignmentUnit is string id) _assignments.Remove(id); RenderBoard(); RenderInspector(); Highlight(); }));
        _decisionActions.AddChild(SidebarSkin.Button("Confirm assignments", () => _ = Submit(new GameCommand(action.Type, View.ViewerId, reviewedVersion, Assignments: new Dictionary<string, int>(_assignments))), true));
        _decisionActions.AddChild(SidebarSkin.Button("Cancel assignment", CancelChoice));
    }

    private void RenderTargeted()
    {
        var action = _chosen!;
        var command = action.Command!;
        var targets = action.TargetIds ?? [];
        var target = command.TargetId ?? targets.FirstOrDefault();
        _inspector.AddChild(Palette.Wrapped(action.Label, 17, Palette.Gold));
        if (targets.Length > 0) _inspector.AddChild(Palette.Wrapped("Click a gold-outlined card on the table or choose below. The arrow shows the selected target; confirm to resolve.", 13, Palette.PaperDim));
        if (command.PerformerId is string performer)
            _inspector.AddChild(Palette.Wrapped("Performer: " + ShortName(FindCard(performer)!) + " · " + CardTile.UnitLabel(FindCard(performer)!), 14, Palette.Crane));
        if (targets.Length > 0)
        {
            _inspector.AddChild(Palette.Text(command.AbilityId is "prepared-death" or "defensive-stance" or "lobby" ? "CHOOSE A PERFORMER" : command.AbilityId == "favor-cycle" ? "CHOOSE A CARD TO DISCARD" : "CHOOSE A TARGET", 11, Palette.Gold));
            var picker = new OptionButton { FitToLongestItem = false, ClipText = true };
            foreach (var id in targets)
            {
                var card = FindCard(id)!;
                // A target-derived cost is part of the choice: each target's own engine quote leads the label.
                var price = action.TargetGold?.TryGetValue(id, out var priced) == true ? $" · {priced.Cost} Gold" : "";
                picker.AddItem($"{(card.Definition?.Type == CardType.Personality ? CardTile.UnitLabel(card) + " · " : "")}{View.Players[card.OwnerId].Clan} · {ShortName(card)}{price} · {card.Force} F{(card.Bowed ? " · bowed" : "")}");
            }
            picker.Selected = Math.Max(0, Array.IndexOf(targets, target));
            picker.ItemSelected += i => ChooseTarget(targets[(int)i]);
            _inspector.AddChild(picker);
        }
        var (required, ready) = RenderGoldPanel(action);
        AddConfirm("Confirm action", required, ready, () => command with { SourceIds = required > 0 ? _sources.ToArray() : [], TargetId = target }, "Cancel action", action.Enabled);
    }

    private void RenderProvinceRefresh()
    {
        var action = _chosen!; var selected = new List<string>();
        _inspector.AddChild(Palette.Wrapped(action.Label, 18, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped("Select one or more province cards in the order you want them placed on the bottom of the Dynasty deck. Replacements will be revealed face-up.", 14, Palette.PaperDim));
        var order = Palette.Wrapped("No cards selected.", 13, Palette.Crane);
        var confirm = SidebarSkin.Button("Refresh selected provinces", () => _ = Submit(action.Command! with { CardIds = selected.ToArray() }), true);
        confirm.Disabled = true;
        foreach (var id in action.TargetIds ?? [])
        {
            var card = FindCard(id)!;
            var check = new CheckBox { Text = $"Province {card.ProvinceIndex + 1} · {ShortName(card)}" };
            check.AddThemeFontSizeOverride("font_size", 13);
            check.Toggled += yes =>
            {
                if (yes) selected.Add(id); else selected.Remove(id);
                confirm.Disabled = selected.Count == 0;
                order.Text = selected.Count == 0 ? "No cards selected." : "Bottom order: " + string.Join(" → ", selected.Select(id => "P" + (FindCard(id)!.ProvinceIndex + 1)));
            };
            _inspector.AddChild(check);
        }
        _inspector.AddChild(order); _decisionActions.AddChild(confirm);
        _decisionActions.AddChild(SidebarSkin.Button("Cancel refresh", CancelChoice));
    }

    private void RenderHandDiscard()
    {
        var count = Self.HandCount - 8;
        var reviewedVersion = _chosen?.Command?.ExpectedVersion ?? View.Version;
        var selected = new HashSet<string>();
        _inspector.AddChild(Palette.Wrapped($"Choose {count} card(s) to discard.", 17));
        var confirm = SidebarSkin.Button("Confirm discard", () => _ = Submit(new GameCommand("discardHand", View.ViewerId, reviewedVersion, CardIds: selected.ToArray())), true);
        confirm.Disabled = true;
        foreach (var c in Self.Hand)
        {
            var check = new CheckBox { Text = ShortName(c) };
            check.Toggled += yes => { if (yes) selected.Add(c.Id); else selected.Remove(c.Id); confirm.Disabled = selected.Count != count; };
            _inspector.AddChild(check);
        }
        _decisionActions.AddChild(confirm);
        _decisionActions.AddChild(SidebarSkin.Button("Cancel selection", CancelChoice));
    }

    private void CancelChoice() { SetNotice("Selection cancelled. No action was taken."); _selected = null; _chosen = null; _sources.Clear(); _assignments.Clear(); _assignmentUnit = null; RenderBoard(); RenderInspector(); RenderPhaseActions(); Highlight(); }

    private void AddAction(VBoxContainer container, ActionOption option)
    {
        var label = option.Type == "discardProvince" ? "Discard from province…" : option.Type is "purchase" or "recruitReserve" ? "Review payment…" : option.Label;
        var button = SidebarSkin.Button(label, () =>
        {
            if (option.Type is "purchase" or "recruitReserve" or "equip" or "playStrategy" or "attack" or "assignDefense" or "discardHand" or "useAbility") Choose(option);
            else if (option.Type == "discardProvince") Confirm("Discard this province card?", "Discarding starts the Dynasty discard step. You cannot recruit more cards this phase.", () => _ = Submit(option.Command!));
            else if (option.Command is { } command) _ = Submit(command);
        }, option.Type is "pass" or "endDynasty" or "declineReaction");
        button.Disabled = !option.Enabled || _session.Busy || _session.Online && !_session.Connected;
        button.TooltipText = RulesHelp.Tooltip((option.Reason is { } reasonText ? "Unavailable: " + reasonText + "\n\n" : option.Label + "\n\n") + RulesHelp.ActionGuide(option.Type));
        button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        button.ClipText = true; container.AddChild(button);
    }

    private void RenderPhaseActions()
    {
        if (PresentationBusy) { Palette.Clear(_phaseActions); _phaseActions.AddChild(SidebarSkin.Button("Review battle", ShowBattleHistory)); return; }
        Palette.Clear(_phaseActions);
        if (_session.Snapshot is null) return;
        if (_battlefieldReviewView is not null)
        {
            _phaseActions.AddChild(Palette.Wrapped("Presentation preview\nUse the prepared scenarios above the table. Card effects are not running.", 17, Palette.PaperDim));
            return;
        }
        if (View.PendingProvinceOrder is { } order)
        {
            _phaseActions.AddChild(Palette.Wrapped(order.PlayerId == View.ViewerId ? "Choose an order in the tray." : "Waiting for the province's controller to choose an order.", 18, Palette.Gold));
            if (_session.Online && !_session.Connected)
                _phaseActions.AddChild(SidebarSkin.Button("Reconnect", () => _ = Reconnect(), true));
            else if (!_session.Online && !_autoOpponent && _provinceReviewView is null && order.PlayerId != View.ViewerId)
                _phaseActions.AddChild(SidebarSkin.Button("Pass device to deciding player", PassProvinceDecision));
            return;
        }
        if (_provinceReviewView is not null)
        {
            _phaseActions.AddChild(Palette.Wrapped("Presentation preview\nUse the prepared scenarios above the table. Card effects are not running.", 17, Palette.PaperDim));
            _phaseActions.AddChild(SidebarSkin.Button("Focus province", FocusProvinceReview));
            return;
        }
        if (View.WinnerId is not null) return;
        if (_chosen is not null) return;
        _phaseActions.AddChild(new HSeparator());
        if (_session.Online && !_session.Connected)
        {
            _phaseActions.AddChild(Palette.Wrapped("Disconnected. Reconnect to continue.", 14, Palette.Error));
            _phaseActions.AddChild(SidebarSkin.Button("Reconnect", () => _ = Reconnect(), true)); return;
        }
        if (_session.Online && (_session.Snapshot.PlayersConnected?.Any(p => !p) ?? false))
        {
            _phaseActions.AddChild(Palette.Wrapped($"Room {_session.Snapshot.RoomCode}\nWaiting for your opponent to connect.", 16, Palette.Crane)); return;
        }
        if (View.Phase == Phase.Battle && View.PendingChoice is null && View.PriorityPlayerId == View.ViewerId && View.Events.LastOrDefault()?.Type == "passed")
            _phaseActions.AddChild(Palette.Wrapped("Passing now ends Battle actions.", 16, Palette.PaperDim));
        foreach (var a in Actions.Where(a => a.Type is "pass" or "endDynasty" or "proclaim" or "declineReaction" or "chooseShinden" or "declareAttack")) AddAction(_phaseActions, a);
        if (View.PriorityPlayerId != View.ViewerId) _phaseActions.AddChild(Palette.Wrapped($"Waiting for {View.Players[View.PriorityPlayerId].Clan}…", 15, Palette.Crane));
        if (!_session.Online && !_autoOpponent) _phaseActions.AddChild(SidebarSkin.Button("Pass device to other player", SwitchSeat));
    }
}
