using Godot;
using L5R.Core;
using L5R.AI;

namespace L5R.Client;

public partial class Main
{
    private int _boardFixtureAttempts;
    private void PrepareBoardCapture()
    {
        _boardFixtureAttempts++;
        var defenseMode = _captureMode.Contains("defense", StringComparison.Ordinal);
        var assignmentMode = defenseMode || _captureMode.Contains("assignment", StringComparison.Ordinal);
        var assignmentType = defenseMode ? "assignDefense" : "attack";
        var minimumUnits = defenseMode ? 1 : 2;
        var targetMode = _captureMode.Contains("target", StringComparison.Ordinal);
        var steps = 0;
        while (View.WinnerId is null && steps++ < 1200)
        {
            _session.FollowPracticePriority();
            var assignment = Actions.FirstOrDefault(a => a.Enabled && a.Type == assignmentType && a.TargetIds?.Length >= minimumUnits);
            var targeted = Actions.FirstOrDefault(a => a.Enabled && a.Type == "useAbility" && a.CardId is not null && a.Command?.CardIds is null && a.TargetIds?.Length > 0 && a.Command?.PerformerId != a.Command?.TargetId && a.CardId != a.Command?.TargetId);
            if ((assignmentMode && assignment is not null || targetMode && View.Phase == Phase.Battle && targeted is not null || !assignmentMode && !targetMode && View.Phase == Phase.Battle && View.Players.Sum(p => p.Army.Count(c => c.AttachedTo is null)) >= 2) && ClarityFixtureReady()) break;
            var command = TrainingOpponent.Choose(View, Actions).Command ?? throw new InvalidOperationException("Board capture has no action.");
            var version = View.Version;
            _session.Submit(command).GetAwaiter().GetResult();
            if (version == View.Version) throw new InvalidOperationException("Board capture action rejected.");
        }
        if (View.WinnerId is not null || steps >= 1200)
        {
            // Fresh practice seeds can win before a suitable battle occurs. Search
            // another real match only during fixture setup; never retry a UI assertion.
            if (_captureMode.Contains("clarity", StringComparison.Ordinal) && _boardFixtureAttempts < 4)
            {
                GD.Print("CLARITY_SETUP: practice ended before the required fixture; trying a fresh match.");
                _session.StartPractice(false, true, 1); PrepareBoardCapture(); return;
            }
            throw new InvalidOperationException("Did not reach the required board capture state.");
        }
        _uiQueue.Clear(); _selected = null; _chosen = null; Render();
        if (assignmentMode)
        {
            var action = Actions.First(a => a.Enabled && a.Type == assignmentType && a.TargetIds?.Length >= minimumUnits);
            var ids = action.TargetIds!.Take(2).ToArray(); var provinces = action.Provinces!; var version = View.Version;
            // Enter assignment by pressing the actual Personality tile, then province controls.
            _tiles.Single(t => t.Card.Id == ids[0]).EmitSignal(Button.SignalName.Pressed);
            _provinceButtons[(1 - View.ActivePlayerId, provinces[0])].EmitSignal(Button.SignalName.Pressed);
            if (ids.Length > 1)
            {
                _tiles.Single(t => t.Card.Id == ids[1]).EmitSignal(Button.SignalName.Pressed);
                _provinceButtons[(1 - View.ActivePlayerId, provinces[^1])].EmitSignal(Button.SignalName.Pressed);
            }
            if (_assignments.Count != ids.Length || View.Version != version || Self.Army.Length != 0) throw new InvalidOperationException("Draft assignment moved authoritative units or lost a selection.");
            // Target selection and panel rebuilding must preserve the other unit's assignment.
            RenderInspector();
            if (_assignments[ids[0]] != provinces[0]) throw new InvalidOperationException("Inspector erased an assignment.");
            CancelChoice();
            if (_assignments.Count != 0 || IntentArrows().Any() || View.Version != version) throw new InvalidOperationException("Cancel did not clear the draft cleanly.");
            _tiles.Single(t => t.Card.Id == ids[0]).EmitSignal(Button.SignalName.Pressed);
            _provinceButtons[(1 - View.ActivePlayerId, provinces[0])].EmitSignal(Button.SignalName.Pressed);
            if (ids.Length > 1)
            {
                _tiles.Single(t => t.Card.Id == ids[1]).EmitSignal(Button.SignalName.Pressed);
                _provinceButtons[(1 - View.ActivePlayerId, provinces[^1])].EmitSignal(Button.SignalName.Pressed);
            }
            if (_captureMode.EndsWith("confirm", StringComparison.Ordinal))
            {
                var planned = new Dictionary<string, int>(_assignments);
                _decisionActions.GetChildren().OfType<Button>().Single(b => b.Text == "Confirm assignments").EmitSignal(Button.SignalName.Pressed);
                _uiQueue.Clear(); Render();
                if (View.Version != version + 1) throw new InvalidOperationException("Assignment confirmation was rejected.");
                foreach (var (id, province) in planned)
                {
                    var unit = FindCard(id)!;
                    if (unit.Zone != Zone.Battlefield || unit.ProvinceIndex != province) throw new InvalidOperationException("Unit did not arrive in its assigned lane.");
                    if (Self.Army.Where(c => c.AttachedTo == id).Any(c => c.ProvinceIndex != province)) throw new InvalidOperationException("Attachments did not travel with their parent.");
                }
            }
            GD.Print($"UI_SMOKE_PASS: {assignmentType}, {ids.Length} independent unit/province drafts, preserved panel selection, cancel; confirmed={_captureMode.EndsWith("confirm", StringComparison.Ordinal)}.");
        }
        if (targetMode)
        {
            var action = Actions.First(a => a.Enabled && a.Type == "useAbility" && a.CardId is not null && a.Command?.CardIds is null && a.TargetIds?.Length > 0 && a.Command?.PerformerId != a.Command?.TargetId && a.CardId != a.Command?.TargetId);
            _selected = FindCard(action.CardId!); Choose(action);
            var targetId = action.TargetIds!.Last(); var version = View.Version;
            _tiles.Single(t => t.Card.Id == targetId).EmitSignal(Button.SignalName.Pressed);
            if (_chosen?.Command?.TargetId != targetId || _chosen.Command.AbilityId != action.Command!.AbilityId || View.Version != version) throw new InvalidOperationException("Direct targeting lost the ability or submitted early.");
            if (_captureMode.EndsWith("confirm", StringComparison.Ordinal))
            {
                _decisionActions.GetChildren().OfType<Button>().Single(b => b.Text.StartsWith("Confirm action", StringComparison.Ordinal)).EmitSignal(Button.SignalName.Pressed);
                _uiQueue.Clear(); Render();
                if (View.Version != version + 1) throw new InvalidOperationException("Targeted action confirmation failed.");
            }
            GD.Print("UI_SMOKE_PASS: direct card target preserves ability/performer and waits for confirmation.");
        }
        // Exercise the actual stack toggle and an individual attached card's press handler.
        if (_captureMode.Contains("expanded", StringComparison.Ordinal))
        {
            var attachment = View.Players.SelectMany(p => p.Army).First(c => c.AttachedTo is not null);
            var parentTile = _tiles.Single(t => t.Card.Id == attachment.AttachedTo);
            var footer = parentTile.GetParent().GetParent().GetChildren().OfType<HBoxContainer>().Single();
            footer.GetChildren().OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
            if (!_expandedUnits.Contains(attachment.AttachedTo!)) throw new InvalidOperationException("Attachment stack did not expand.");
            _tiles.Single(t => t.Card.Id == attachment.Id).EmitSignal(Button.SignalName.Pressed);
            if (_selected?.Id != attachment.Id || _selected.AttachedTo != attachment.AttachedTo) throw new InvalidOperationException("Expanded attachment could not be inspected independently.");
            GD.Print("UI_SMOKE_PASS: stack toggle spreads attachments; an individual attachment opens in the inspector with its parent preserved.");
        }
        RenderBoard(); ResetTableCamera();
        if (_captureMode.Contains("clarity", StringComparison.Ordinal)) PrepareClarityCapture();
        if (_captureMode.Contains("piles", StringComparison.Ordinal)) ValidatePileControls();
        if (_captureMode.EndsWith("settings", StringComparison.Ordinal)) ShowSettings();
        if (_captureMode.Contains("gate", StringComparison.Ordinal))
            _UnhandledKeyInput(new InputEventKey { Keycode = Key.F11, Pressed = true });
        if (_captureMode.Contains("motionoff", StringComparison.Ordinal))
        {
            ShowSettings();
            var dialog = GetChildren().OfType<AcceptDialog>().LastOrDefault();
            var motion = (dialog as Node ?? GetTree().Root).FindChildren("*", "CheckBox", true, false).OfType<CheckBox>().Single(b => b.Text.StartsWith("Reduced motion", StringComparison.Ordinal));
            motion.ButtonPressed = true;
            if (!_reducedMotion || !_table.ReducedMotion) throw new InvalidOperationException("Reduced motion did not stop environmental animation.");
            if (dialog is not null) dialog.QueueFree();
            else
            {
                // The menu branch hosts Options in its own canvas rather than a dialog.
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
                GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
                if (!_root.Visible) throw new InvalidOperationException("Options did not return to the existing table.");
            }
        }
    }

    private void ValidateBoardCapture()
    {
        GD.Print($"RENDER_METRICS: fps={Performance.GetMonitor(Performance.Monitor.TimeFps)}, table_texture={_tableSurface.Canvas.Size}, density={_tableSurface.RenderScale}");
        ValidateTurnAndClarity();
        ValidateQuietHud();
        ValidatePhysicalLayout();
        if (_captureMode.EndsWith("piletip", StringComparison.Ordinal))
        {
            if (ReadingTooltip.LastText?.Contains($"{Self.HandCount} in hand", StringComparison.Ordinal) != true) throw new InvalidOperationException($"Native pile hover did not expose the public hand count. focus={GetWindow().HasFocus()}, hover={TableHovered?.Name}, target={_tableHover?.Name}, time={_tableHoverTime}, pointer={_lastTableMouse}, text={ReadingTooltip.LastText}");
            GD.Print("UI_SMOKE_PASS: actual pile hover opens a scaled count tooltip, including hand count without card identities.");
        }
        if (_captureMode.Contains("pages", StringComparison.Ordinal)) ValidateShelfPaging();
        if (_captureMode.Contains("motionoff", StringComparison.Ordinal))
        {
            _table.PresentLight(0, !_table.ReducedMotion); _timeline.PresentLight(0, !_table.ReducedMotion); RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
            var first = GetViewport().GetTexture().GetImage().GetData();
            _table.PresentLight(4, !_table.ReducedMotion); _timeline.PresentLight(4, !_table.ReducedMotion); RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
            var second = GetViewport().GetTexture().GetImage().GetData();
            if (!first.AsSpan().SequenceEqual(second)) throw new InvalidOperationException("Reduced-motion frame still changes with candle phase.");
            GD.Print("UI_SMOKE_PASS: settings toggle stops candle animation; independent light phases render identical frames.");
        }
        if (_captureMode.Contains("gate", StringComparison.Ordinal))
        {
            var resumed = _captureMode.EndsWith("resume", StringComparison.Ordinal);
            if (resumed != IsFullscreen || _fullscreenGate.Visible) throw new InvalidOperationException("Optional fullscreen toggle disagrees with window state.");
            GD.Print($"UI_SMOKE_PASS: windowed play preserves the match without a fullscreen gate; resumed={resumed}.");
        }
        var expected = View.Players.SelectMany(p => p.Home.Concat(p.Army).Append(p.Stronghold).Concat(p.Provinces.Where(q => q.Card is not null).Select(q => q.Card!))).Concat(Self.Hand).Select(c => c.Id).Order().ToArray();
        var actual = _tiles.Select(t => t.Card.Id).Order().ToArray();
        if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Visible board duplicated or omitted a card.");
        foreach (var tile in _tiles)
        {
            if (tile.IsVisibleInTree())
            {
                var corners = tile.FaceCornersOnViewport();
                if (_cameraFitted && corners.Any(p => !GetViewport().GetVisibleRect().Grow(1).HasPoint(p))) throw new InvalidOperationException($"Card {tile.Card.Id} extends beyond the viewport.");
                for (Node? ancestor = tile.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
                    if (ancestor is CardShelf shelf && corners.Any(p => !shelf.GetGlobalRect().Grow(2).HasPoint(p)))
                        throw new InvalidOperationException($"Card {tile.Card.Id} is clipped by its shelf: {shelf.GetGlobalRect()}.");
                if (!tile._HasPoint(tile.Size / 2) || tile._HasPoint(new Vector2(-20, -20))) throw new InvalidOperationException("Card hit region does not match the visible face.");
                var inverse = tile.GetGlobalTransform().AffineInverse();
                var local = corners.Select(p => inverse * (tile.ProjectToScreen is null ? p : _tableSurface.ScreenToCanvas(p))).ToArray();
                var center = local.Aggregate(Vector2.Zero, (a, b) => a + b) / 4;
                for (var edge = 0; edge < 4; edge++)
                {
                    var midpoint = (local[edge] + local[(edge + 1) % 4]) / 2;
                    var outward = (midpoint - center).Normalized();
                    var outside = midpoint + outward * 8;
                    // A lifted hand intentionally retains its resting pick area to avoid edge-hover oscillation.
                    var retainedHandArea = tile.Card.Zone == Zone.Hand && tile.IsInspectionHover && tile.ContainsRestingFace(outside);
                    if (!tile._HasPoint(midpoint - outward * 4) || tile._HasPoint(outside) && !retainedHandArea)
                        throw new InvalidOperationException("Projected card edge does not match its input polygon.");
                }
            }
            if (!Mathf.IsEqualApprox(tile.VisualBowAngle, tile.Card.Bowed ? Mathf.Pi / 2 : 0)) throw new InvalidOperationException("Card orientation does not match its own bowed state.");
            if (tile.Card.AttachedTo is string parent && tile.GetParent().GetMeta("unit_id").AsString() != parent) throw new InvalidOperationException("Attachment displayed under the wrong Personality.");
            if (tile.Card.Zone == Zone.Battlefield)
            {
                Node? lane = tile;
                while (lane is not null && !lane.HasMeta("battle_province")) lane = lane.GetParent();
                if (lane is null || lane.GetMeta("battle_province").AsInt32() != tile.Card.ProvinceIndex) throw new InvalidOperationException("Battlefield card rendered in the wrong lane.");
            }
            if (tile.Card.Zone == Zone.Home)
            {
                Node? home = tile;
                while (home is not null && !home.HasMeta("home_owner")) home = home.GetParent();
                if (home is null || home.GetMeta("home_owner").AsInt32() != tile.Card.OwnerId) throw new InvalidOperationException("Home card rendered outside its owner's home.");
            }
        }
        if (_hoverProbeCard is not null)
        {
            if (CardTile.LastPreviewCardId != _hoverProbeCard) throw new InvalidOperationException("Real mouse hover did not open the expected card preview.");
            var previewScale = GetViewport().GetVisibleRect().Size.Y / 1080f;
            if (CardTile.LastPreviewControl is not { } preview || !IsInstanceValid(preview) || preview.Size.X > 770 * previewScale || preview.Size.Y > 590 * previewScale) throw new InvalidOperationException("Hover preview exceeded its bounded layout.");
            if (View.Version != _hoverProbeVersion || _chosen?.Command != _hoverProbeCommand) throw new InvalidOperationException("Hover changed the match or selected action.");
            var hidden = _tiles.FirstOrDefault(t => t.Card.Definition is null);
            if (hidden is not null)
            {
                var tooltip = hidden.CreateReadingPreview();
                bool HasImage(Node n) => n is TextureRect || n.GetChildren().Any(HasImage);
                if (HasImage(tooltip)) throw new InvalidOperationException("Hidden card preview exposed an image.");
                tooltip.Free();
            }
            GD.Print("UI_SMOKE_PASS: native hover opened a full-card preview, preserved the pending action and did not reveal hidden art.");
        }
        var fabric = TableArtStage.FabricBounds(new Vector2(WorldWidth, _worldHeight));
        var padded = fabric.Grow(-TableArtStage.FabricPadding + 2);
        foreach (var tile in _tiles.Where(t => _world.IsAncestorOf(t)))
            if (tile.FaceCornersOnViewport().Any(p => !padded.HasPoint(ScreenToWorld(p))))
                throw new InvalidOperationException("A board card lacks navy mat padding.");
        foreach (var pile in TablePiles)
            if (PileCorners(pile).Any(p => !padded.HasPoint(ScreenToWorld(p * _root.Scale.X))))
                throw new InvalidOperationException("A pile lacks navy mat padding.");
        GD.Print($"UI_SMOKE_PASS: all board card and pile corners have at least {TableArtStage.FabricPadding - 2} logical units of navy fabric clearance.");
        var viewport = GetViewport().GetVisibleRect();
        var presentation = _root.GetGlobalRect();
        if (presentation.Position.Length() > 1 || presentation.Size.DistanceTo(viewport.Size) > 2)
            throw new InvalidOperationException($"Presentation does not fill the viewport: {presentation} vs {viewport}.");
        if (_captureMode.Contains("fullscreen", StringComparison.Ordinal) && !_captureMode.Contains("gate", StringComparison.Ordinal) && !IsFullscreen)
            throw new InvalidOperationException("Fullscreen capture did not enter fullscreen.");
        GD.Print($"PRESENTATION: window={GetWindow().Mode}, viewport={viewport.Size}, sidebar={_inspectorPanel.GetGlobalRect().Size}, full_width=true");
        foreach (var zone in new[] { Zone.Home, Zone.Province, Zone.Battlefield, Zone.Hand })
        {
            var heights = _tiles.Where(t => t.Card.Zone == zone && t.IsVisibleInTree()).Select(t => t.FaceCornersOnViewport()[0].DistanceTo(t.FaceCornersOnViewport()[3])).ToArray();
            if (heights.Length > 0) GD.Print($"CARD_FACE_HEIGHT: {zone}, min={heights.Min():F1}, max={heights.Max():F1} pixels");
        }
        if (_cameraFitted && new[] { Vector2.Zero, new Vector2(WorldWidth, 0), new Vector2(WorldWidth, _worldHeight), new Vector2(0, _worldHeight) }.Any(p => !_tableViewport.GetGlobalRect().Grow(1).HasPoint(WorldToScreen(p))))
            throw new InvalidOperationException("Projected board does not fit its display viewport.");
        if (_chosen is not null && _decisionActions.GetChildren().OfType<Button>().Any(b => b.GetGlobalRect().End.Y > viewport.End.Y)) throw new InvalidOperationException("A confirmation button is outside the viewport.");
        GD.Print($"UI_SMOKE_PASS: {actual.Length} card nodes correctly oriented, grouped and placed exactly once; table uses uncropped card faces; confirmation controls fit {viewport.Size}.");
    }
    private void ValidateShelfPaging()
    {
        var version = View.Version;
        IEnumerable<CardTile> Cards(Node node) => node.GetChildren().SelectMany(child => child is CardTile card ? new[] { card } : Cards(child));
        IEnumerable<Node> Descendants(Node node) => node.GetChildren().SelectMany(child => new[] { child }.Concat(Descendants(child)));
        var shelves = Descendants(_board).OfType<CardShelf>().ToArray();
        var exercised = 0;
        foreach (var shelf in shelves)
        {
            var all = Cards(shelf.Items).Select(t => t.Card.Id).ToHashSet();
            var seen = Cards(shelf.Items).Where(t => t.IsVisibleInTree()).Select(t => t.Card.Id).ToHashSet();
            var next = shelf.GetNode<Button>("NextPage"); var previous = shelf.GetNode<Button>("PreviousPage");
            if (!next.Visible) continue;
            while (!next.Disabled)
            {
                next.EmitSignal(Button.SignalName.Pressed);
                seen.UnionWith(Cards(shelf.Items).Where(t => t.IsVisibleInTree()).Select(t => t.Card.Id));
            }
            while (!previous.Disabled) previous.EmitSignal(Button.SignalName.Pressed);
            if (!all.SetEquals(seen)) throw new InvalidOperationException("A card cannot be reached through shelf paging.");
            exercised++;
        }
        if (exercised == 0 || View.Version != version) throw new InvalidOperationException("Paging did not exercise a crowded shelf or changed the match.");
        GD.Print($"UI_SMOKE_PASS: {exercised} crowded shelves expose every card through real page controls, without changing the match.");
    }

    private static IEnumerable<Node> HudDescendants(Node node) => node.GetChildren().SelectMany(child => new[] { child }.Concat(HudDescendants(child)));

    private void ValidateQuietHud()
    {
        var forbidden = new[] { "YOUR HOME", "OPPONENT HOME", "ready Gold", "discarded", "Destroyed", "attacking", "defending", "CURRENT BATTLE" };
        foreach (var node in HudDescendants(_board))
        {
            var text = node is Label label ? label.Text : node is Button button ? button.Text : "";
            if (forbidden.Any(word => text.Contains(word, StringComparison.Ordinal))) throw new InvalidOperationException("A removed board label returned: " + text);
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+ F\s*·\s*#|\d+ / \d+|(?:Crane|Lion) P\d")) throw new InvalidOperationException("A unit summary or numeric page counter returned.");
        }
        if (_clanHud.GetChildren().OfType<ClanPlaque>().Count() != 2 || _pileHud.GetChildren().OfType<TablePile>().Count() != 8) throw new InvalidOperationException("Clan plaques or physical piles are missing.");
        foreach (var player in View.Players)
            foreach (var province in player.Provinces.Where(p => p.Destroyed))
                if (_provinceButtons.ContainsKey((player.Id, province.Index))) throw new InvalidOperationException("A destroyed province still occupies a board control.");
        if (_inspectorPanel.Size.X != 300) throw new InvalidOperationException("Compact inspector width regressed.");
        GD.Print("UI_SMOKE_PASS: two clan resource plaques, eight physical piles, compact inspector; removed board summaries and destroyed-province controls remain absent.");
    }

    private void ValidatePileControls()
    {
        var version = View.Version;
        foreach (var pile in _pileHud.GetChildren().OfType<TablePile>())
        {
            var player = View.Players.Single(p => p.Id == pile.OwnerId);
            var expected = player.Discard.Where(c => c.Zone == pile.PileZone).Select(c => c.Id).Order().ToArray();
            pile.EmitSignal(Button.SignalName.Pressed);
            var dialog = GetChildren().OfType<AcceptDialog>().Last(d => !d.IsQueuedForDeletion());
            var actual = HudDescendants(dialog).OfType<CardTile>().Select(t => t.Card.Id).Order().ToArray();
            if (!expected.SequenceEqual(actual)) throw new InvalidOperationException("Pile inspection exposed a deck card or omitted a discarded card.");
            var count = pile.PileZone switch { Zone.DynastyDeck => player.DynastyDeckCount, Zone.FateDeck => player.FateDeckCount, _ => expected.Length };
            if (pile.Count != count || !pile.TooltipText.Contains($"{count} cards", StringComparison.Ordinal)) throw new InvalidOperationException("Pile hover count disagrees with the projected view.");
            dialog.Hide(); dialog.QueueFree();
        }
        var badge = _clanHud.GetChildren().OfType<ClanPlaque>().Last();
        badge.GetChildren().OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
        var fallen = GetChildren().OfType<AcceptDialog>().Last(d => !d.IsQueuedForDeletion());
        if (HudDescendants(fallen).OfType<CardTile>().Any(t => t.Card.Zone != Zone.Dead)) throw new InvalidOperationException("Fallen-card archive mixed zones.");
        fallen.Hide(); fallen.QueueFree();
        _pileHud.GetChildren().OfType<TablePile>().First(p => p.PileZone == Zone.DynastyDiscard && p.Count > 0).EmitSignal(Button.SignalName.Pressed);
        if (View.Version != version) throw new InvalidOperationException("Inspecting a pile changed the match.");
        GD.Print("UI_SMOKE_PASS: all eight real pile controls match public counts and contents; decks reveal no identities; clan archive opens; inspection leaves match unchanged.");
    }

}
