using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #954 (table clicks, phase 2): the first click on one of your cards in play starts its ability when exactly one can be used; hovering inspects. Fixtures are real published cards in a prepared state.
public partial class Main
{
    private bool _tableClickStarted;

    private void ProcessTableClickCapture(double delta)
    {
        _captureClock += delta; if (_tableClickStarted || _captureClock < 1.5) return;
        _tableClickStarted = true; _ = CheckTableClick();
    }

    private async Task CheckTableClick()
    {
        try
        {
            // 1. Agasha Kodo's only ability ("Target a Holding: Straighten it") starts from the Personality, and the bowed Holding is then a highlighted target on the table.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 142, 0, Zone.Home); SupportFixtureCard(s, 4664, 0, Zone.Home, bowed: true); });
            var kodo = View.Players[0].Home.Single(c => c.Definition?.Name == "Agasha Kodo");
            var usable = Actions.Where(a => a.Enabled && a.CardId == kodo.Id && a.Type == "useAbility").ToArray();
            SupportRequire(usable.Length == 1, $"Agasha Kodo should have one usable ability: {usable.Length} / " + string.Join(" | ", Actions.Where(a => a.CardId == kodo.Id).Select(a => $"{a.Type}/{a.Enabled}/{a.Reason}")));
            SelectCard(kodo); await EventFrames(4);
            SupportRequire(_chosen is { Type: "useAbility" } && _chosen.Command?.AbilityId == "kodo-straighten", "Clicking the Personality did not start its ability");
            var holding = View.Players[0].Home.First(c => c.Definition?.Type == CardType.Holding && c.Bowed);
            SupportRequire(_tiles.Single(t => t.Card.Id == holding.Id).LegalTarget, "The bowed Holding is not highlighted as the target");
            SelectCard(holding); await EventFrames(3);
            SupportRequire(_chosen?.Command?.TargetId == holding.Id, "Clicking the Holding did not choose it as the target");

            // 2. Taruko's ability has nothing to choose: the click opens the action for its final Confirm.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 4537, 0, Zone.Home, bowed: true); });
            var taruko = View.Players[0].Home.Single(c => c.Definition?.Name == "Komori Taruko");
            SelectCard(taruko); await EventFrames(4);
            SupportRequire(_chosen?.Command?.AbilityId == "taruko-straighten", "Clicking Taruko did not start her ability");

            // 3. A Personality with no usable ability is simply selected, as before; a second click deselects.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 229, 0, Zone.Home); });
            var plain = View.Players[0].Home.Single(c => c.Definition?.Type == CardType.Personality);
            SupportRequire(!Actions.Any(a => a.Enabled && a.CardId == plain.Id && a.Type == "useAbility"), "The control Personality unexpectedly has a usable ability");
            SelectCard(plain); await EventFrames(3);
            SupportRequire(_chosen is null && _selected?.Id == plain.Id, "A card with no usable ability should just be selected");
            SelectCard(plain); await EventFrames(3);
            SupportRequire(_selected is null, "A second click should deselect");

            // 4. Not while the opponent holds priority or while another action is being reviewed.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; s.PriorityPlayerId = 1; SupportFixtureCard(s, 4537, 0, Zone.Home, bowed: true); });
            var waiting = View.Players[0].Home.Single(c => c.Definition?.Name == "Komori Taruko");
            SelectCard(waiting); await EventFrames(3);
            SupportRequire(_chosen is null, "A click must not start an ability out of turn");

            // 5. A Celestial (a support card) starts its only usable ability from its tile too: Akodo's Guidance, in a battle, is a Melee Attack for a Lion player.
            await LoadDirectPlay(s => { BattleSetup(s); s.Players[0].Clan = "Lion"; SupportFixtureCard(s, 311, 0, Zone.Celestial); });
            var guidance = View.Players[0].Celestials!.Single();
            var guidanceUsable = Actions.Where(a => a.Enabled && a.CardId == guidance.Id && a.Type == "useAbility").ToArray();
            SupportRequire(guidanceUsable.Length >= 1, "Akodo's Guidance has no usable ability in the prepared battle: " + string.Join(" | ", Actions.Where(a => a.CardId == guidance.Id).Select(a => $"{a.Type}/{a.Enabled}/{a.Reason}")));
            SelectCard(guidance); await EventFrames(4);
            SupportRequire(_chosen is { Type: "useAbility" } && _chosen.CardId == guidance.Id, "Clicking the Celestial did not start its ability");

            // 6. Issue #956: an ability whose performer is one of several Personalities (Bayushi Suwabe: "Target your performing Courtier") starts from the card, and the performer is chosen by clicking a highlighted Personality.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 901, 0, Zone.Home); SupportFixtureCard(s, 142, 0, Zone.Home); SupportFixtureCard(s, 142, 0, Zone.Home); });
            var suwabe = View.Players[0].Home.Single(c => c.Definition?.Name == "Bayushi Suwabe");
            var suwabeOptions = Actions.Where(a => a.Enabled && a.CardId == suwabe.Id && a.Type == "useAbility").ToArray();
            SupportRequire(suwabeOptions.Select(a => a.Command!.PerformerId).Distinct().Count() == 2, "Suwabe should be performable by either Courtier: " + string.Join(" | ", Actions.Where(a => a.CardId == suwabe.Id).Select(a => $"{a.Type}/{a.Command?.PerformerId}/{a.Enabled}/{a.Reason}")));
            SelectCard(suwabe); await EventFrames(4);
            SupportRequire(_chosen?.Command?.AbilityId == "suwabe-force", "Clicking Suwabe did not start his ability");
            var performers = PerformerClicks();
            SupportRequire(performers.Count == 2, $"Both Courtiers should be clickable performers, found {performers.Count}");
            var other = performers.Keys.Single(id => id != _chosen!.Command!.PerformerId);
            SupportRequire(_tiles.Single(t => t.Card.Id == other).LegalTarget, "The other Courtier is not highlighted on the table");
            await SupportClick(_tiles.Single(t => t.Card.Id == other)); await EventFrames(6);
            SupportRequire(_chosen?.Command?.PerformerId == other, "Clicking the Courtier did not choose it as the performer");

            // 7. Issue #958: while a province is being chosen, clicking the Dynasty card in a province chooses that province (Shrine to Nimuro: "Target a province").
            await LoadDirectPlay(s =>
            {
                s.Phase = Phase.Action;
                SupportFixtureCard(s, 7227, 0, Zone.Home);
                var tactician = SupportFixtureCard(s, 10292, 0, Zone.Home);
                tactician.GrantedKeywords.Add(new("Lion Clan", "test-fixture", GrantDuration.Permanent)); tactician.GrantedKeywords.Add(new("Tactician", "test-fixture", GrantDuration.Permanent));
                s.Cards[s.Players[1].Provinces.First(p => !p.Destroyed && p.CardId is not null).CardId!].FaceUp = true;
            });
            var shrine = View.Players[0].Home.Single(c => c.Definition?.Name == "Shrine to Nimuro");
            SelectCard(shrine); await EventFrames(4);
            SupportRequire(ProvinceTargetActive && _chosen!.Command!.Province is null, "Clicking the Shrine did not start choosing a province");
            var foe = View.Players.First(p => p.Id != View.ViewerId);
            var faceUp = foe.Provinces.First(p => !p.Destroyed && p.Card is { FaceUp: true, Definition: not null });
            SelectCard(faceUp.Card!); await EventFrames(4);
            SupportRequire(_chosen!.Command is { Province: { } picked, TargetPlayerId: { } pickedOwner } && picked == faceUp.Index && pickedOwner == foe.Id, $"Clicking the province's card did not choose the province (got {_chosen.Command?.TargetPlayerId}/{_chosen.Command?.Province})");

            // 8. Issue #962: in the announce step of a window both players hold a Reaction for, clicking the card announces its Reaction and clicking it again retracts it.
            {
                var state = MatchEngine.CreateEconomyScenario().State;
                foreach (var p in state.Players) { state.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; foreach (var id in p.Hand.ToArray()) state.Cards.Remove(id); p.Hand.Clear(); }
                state.Phase = Phase.BattleSelection; state.ActivePlayerId = state.PriorityPlayerId = 0; state.CurrentBattleProvince = null; state.UnresolvedBattlefields = [0];
                var mine = SupportFixtureCard(state, "training-crane-samurai", 0, Zone.Battlefield, 0); SupportFixtureCard(state, "training-lion-samurai", 1, Zone.Battlefield, 0);
                var heldA = SupportFixtureCard(state, "training-engage-honor-strategy", 0, Zone.Hand); state.Players[0].Hand.Add(heldA.Id);
                var heldB = SupportFixtureCard(state, "training-engage-honor-strategy", 0, Zone.Hand); state.Players[0].Hand.Add(heldB.Id);
                var theirs = SupportFixtureCard(state, "training-engage-honor-strategy", 1, Zone.Hand); state.Players[1].Hand.Add(theirs.Id);
                var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [state])!;
                SupportRequire(engine.Execute(new GameCommand("selectBattle", 0, engine.Version, Province: 0)).Accepted, "Could not open the battle");
                await LoadSupportFixture(engine, 0);
                SupportRequire(View.ReactionOrder is { Stage: "Announce" }, "The announce step did not open: " + View.PendingChoice?.Type);
                SupportRequire(_tiles.Where(t => t.Card.Id == heldA.Id).All(t => t.LegalTarget), "The Reaction's card is not highlighted on the table");
                SelectCard(FindCard(heldA.Id)!); await EventFrames(6);
                SupportRequire(View.ReactionOrder?.Announced.Any(a => a.CardId == heldA.Id) == true, "Clicking the card did not announce its Reaction");
                SelectCard(FindCard(heldA.Id)!); await EventFrames(6);
                SupportRequire(View.ReactionOrder?.Announced.All(a => a.CardId != heldA.Id) == true, "Clicking an announced card did not retract it");
            }

            // 9. Issue #965: a target in a discard pile is chosen from the pile. A Yojimbo's Duty recruits a discarded Courtier or Shugenja (Agasha Kodo) by destroying a performing Yojimbo (Bayushi Suwabe).
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 901, 0, Zone.Home); SupportFixtureCard(s, 142, 0, Zone.DynastyDiscard); SupportFixtureCard(s, 101, 0, Zone.Hand); });
            var duty = View.Players[0].Hand.Single();
            var kodoInPile = View.Players[0].Discard.Single(c => c.Definition?.Name == "Agasha Kodo");
            SelectCard(duty); await EventFrames(4);
            if (_chosen is null) { await SupportClick(ActionTextBlocks().Single(b => b.Text.Contains("Recruit", StringComparison.Ordinal))); await EventFrames(4); } // two usable actions: the player picks the one that recruits
            SupportRequire(_chosen?.Command?.AbilityId == "yojimbos-duty-recruit" && _chosen.TargetIds?.Contains(kodoInPile.Id) == true, $"A Yojimbo's Duty did not begin with the discarded Courtier as a legal target (chosen {_chosen?.Command?.AbilityId}; targets {string.Join(",", Actions.Where(a => a.Enabled && a.CardId == duty.Id).SelectMany(a => a.TargetIds ?? []))}; kodo {kodoInPile.Id}; prompt {_playPrompt is not null}; choices {PlayChoices(duty).Length}; locked {duty.ActionLocked}; selected {_selected?.Id}; duty {duty.Id}; pending {View.PendingChoice?.Type}; priority {View.PriorityPlayerId}; blocks {ActionTextBlocks().Count()})");
            var dynastyPile = _pileHud.GetChildren().OfType<TablePile>().Single(pl => pl.OwnerId == 0 && pl.PileZone == Zone.DynastyDiscard);
            SupportRequire(dynastyPile.Modulate.R > 1.05f, "The Dynasty discard pile is not tinted although it holds the legal target");
            ShowPile(0, Zone.DynastyDiscard); await EventFrames(6);
            var pileTile = GetTree().Root.FindChildren("*", "Button", true, false).OfType<CardTile>().Single(t => t.Card.Id == kodoInPile.Id && !_tiles.Contains(t));
            SupportRequire(pileTile.LegalTarget, "The legal target is not marked inside the pile");
            pileTile.EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(6);
            SupportRequire(_chosen?.Command?.TargetId == kodoInPile.Id, "Clicking the card in the pile did not choose it as the target");

            // 10. Issue #965: an action played from the discard pile starts from the pile (Scandalous Gossip), and its player is then chosen on the table.
            await LoadDirectPlay(s => { s.Phase = Phase.Action; SupportFixtureCard(s, 10391, 0, Zone.FateDiscard); SupportFixtureCard(s, 229, 1, Zone.Home); });
            var gossip = View.Players[0].Discard.Single(c => c.Definition?.Name == "Scandalous Gossip");
            SupportRequire(Actions.Any(a => a.Enabled && a.CardId == gossip.Id), "Scandalous Gossip is not playable from the discard pile: " + string.Join(" | ", Actions.Where(a => a.CardId == gossip.Id).Select(a => $"{a.Type}/{a.Enabled}/{a.Reason}")));
            ShowPile(0, Zone.FateDiscard); await EventFrames(6);
            var gossipTile = GetTree().Root.FindChildren("*", "Button", true, false).OfType<CardTile>().Single(t => t.Card.Id == gossip.Id && !_tiles.Contains(t));
            SupportRequire(gossipTile.LegalTarget, "The playable card is not marked inside the pile");
            gossipTile.EmitSignal(BaseButton.SignalName.Pressed); await EventFrames(6);
            SupportRequire(_chosen?.Command?.AbilityId == "wl-scandalous-gossip" && !PlayerPicked, "Clicking the card in the discard pile did not start its action");
            SelectCard(View.Players.First(p => p.Id != View.ViewerId).Stronghold); await EventFrames(4);
            SupportRequire(_chosen?.Command?.TargetPlayerId == 1, "The player was not chosen by clicking the stronghold");

            // 11. Issue #973: a unit assigned to a battlefield goes home when it is clicked again.
            {
                var state = MatchEngine.CreatePublishedTestMatch(42).State;
                foreach (var p in state.Players) { foreach (var id in p.Hand) state.Cards.Remove(id); p.Hand.Clear(); p.TurnsTaken = 2; }
                state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null; state.Phase = Phase.AttackDeclaration;
                SupportFixtureCard(state, 229, 0, Zone.Home);
                var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [state])!;
                SupportRequire(engine.Execute(new GameCommand("declareAttack", 0, engine.Version)).Accepted, "Could not declare the attack");
                await LoadSupportFixture(engine, 0);
                var attacker = View.Players[0].Home.Single(c => c.Definition?.Type == CardType.Personality);
                var battlefield = Actions.First(a => a.Enabled && a.Type == "attack").Provinces![0];
                SelectCard(attacker); await EventFrames(4);
                SelectProvince(1, battlefield); await EventFrames(4);
                SupportRequire(_assignments.TryGetValue(attacker.Id, out var sentTo) && sentTo == battlefield, "The unit was not assigned to the battlefield");
                SelectCard(attacker); await EventFrames(4);
                SupportRequire(!_assignments.ContainsKey(attacker.Id), "Clicking the assigned unit again did not send it home");
                SelectProvince(1, battlefield); await EventFrames(4);
                SupportRequire(_assignments.TryGetValue(attacker.Id, out sentTo) && sentTo == battlefield, "The unit could not be assigned again after going home");
            }

            GD.Print("TABLE_CLICK_PASS: a card in play with one usable ability starts it on the first click and its target is clicked on the table; an ability with nothing to choose opens for its final Confirm; a Celestial starts its ability from its tile; an ability with several possible performers is performed by clicking one; a province is chosen by clicking its card; a Reaction is announced and retracted by clicking its card; a target in a discard pile and an action played from it are chosen from the pile; an assigned unit goes home when clicked again; a card with no usable ability is only selected; nothing starts out of turn.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
