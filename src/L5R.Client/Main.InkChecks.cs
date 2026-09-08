using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private bool _inkProbeDone;
    private string? _inkProbeError;

    /// <summary>
    /// Runs in the native capture scene. Real-board checks use its actual view,
    /// layout and card signals. Isolated animation checks use representative
    /// rectangles, not fabricated gameplay events or an altered rules snapshot.
    /// </summary>
    private async void ProbeInkMat()
    {
        try
        {
            // Let deferred container layout, camera and the preceding fixture's
            // legitimate ink transition finish before recording resting bounds.
            await ToSignal(GetTree().CreateTimer(3.8), SceneTreeTimer.SignalName.Timeout);
            if (_matSections.IsAnimating) throw new InvalidOperationException("Real mat did not settle after fixture setup.");
            ValidateInkBoardTopology();
            ValidateInkCardClearance();
            var version = View.Version;
            var targets = _matTargets.ToDictionary(s => s.Id, s => s);
            var resting = targets.Keys.ToDictionary(id => id, id => InkBounds(_matSections, id));

            RenderBoard();
            AssertInkLayoutUnchanged(targets, resting, "same-state RenderBoard");
            if (_chosen is not null || _session.Busy)
                throw new InvalidOperationException("Ink inspection probe requires a settled battle without an active action draft.");
            DeselectCard();
            var holding = _tiles.First(t => t.Card.OwnerId == Self.Id && t.Card.Zone == Zone.Home && t.Card.Definition?.Type == CardType.Holding);
            var holdingId = holding.Card.Id;
            holding.EmitSignal(Control.SignalName.MouseEntered);
            holding._Process(.2);
            if (!holding.IsInspectionHover || holding.InteractionScale <= 1)
                throw new InvalidOperationException("Holding hover signal did not activate the existing card feedback.");
            AssertInkLayoutUnchanged(targets, resting, "Holding hover");
            holding.EmitSignal(Control.SignalName.MouseExited);
            holding.EmitSignal(Button.SignalName.Pressed);
            if (_selected?.Id != holdingId || _chosen is not null)
                throw new InvalidOperationException("Holding inspection signal did not select without creating a game draft.");
            RenderBoard();
            AssertInkLayoutUnchanged(targets, resting, "selected-card RenderBoard");
            _tiles.Single(t => t.Card.Id == holdingId).EmitSignal(Button.SignalName.Pressed);
            if (_selected is not null || _chosen is not null)
                throw new InvalidOperationException("Repeated Holding inspection did not deselect safely.");
            AssertInkLayoutUnchanged(targets, resting, "card deselection");
            if (View.Version != version) throw new InvalidOperationException("Mat inspection checks changed the authoritative match version.");
            ReleaseTableFocus(); HideTableTooltip();
            ValidateInkBoardTopology();
            ValidateInkAttachmentTrays(targets, resting, version);
            ValidateEmptyProvinceThreshold();
            await ValidateMatEmblems();
            ValidateInkTransitions();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
            ValidateInkCardClearance();
            if (_captureMode.EndsWith("provinces", StringComparison.Ordinal))
            {
                // Honest close inspection through the ordinary camera transform;
                // the captured match, cards and attachment membership stay intact.
                var bays = _matTargets.Where(s => s.Id.StartsWith($"province:{Self.Id}:", StringComparison.Ordinal)).OrderBy(s => s.Bounds.Position.X).Take(2).ToArray();
                var region = bays.Select(s => s.Bounds).Aggregate((a,b) => a.Merge(b)).Grow(32);
                _cameraCenter = region.GetCenter(); _cameraZoom = FitRegion(region); _cameraFitted = false; ApplyTableCamera();
                _matSections.Emphasize(bays[0].Id);
                await ToSignal(GetTree().CreateTimer(.45), SceneTreeTimer.SignalName.Timeout);
            }
            GD.Print($"INK_MAT_PASS: {_matTargets.Count} icon-marked, disjoint sections; in-play Holdings left for both seats; exact visible-card membership; real scene hover/selection/deselection, attachment tray expansion/folding and same-state rebuild preserve settled borders and match version.");
            GD.Print("INK_MOTION_PASS: isolated native renderer verifies card-settlement gating, expansion/contraction apparition, rapid retarget, unchanged-sync reveal progress, delayed removal, immediate reduced motion and new-match identity reset. Representative geometry only; no invented game events.");
        }
        catch (Exception error) { _inkProbeError = error.ToString(); }
        finally { _inkProbeDone = true; }
    }

    private void ValidateInkBoardTopology()
    {
        InkRequire(_matTargets.Count > 0 && _matTargets.Select(s => s.Id).Distinct().Count() == _matTargets.Count,
            "Mat sections are missing or repeat an identity.");
        InkRequire(_matTargets.All(s => !string.IsNullOrWhiteSpace(s.Label)), "A mat section has no accessible name.");
        InkRequire(_matSections.MouseFilter == Control.MouseFilterEnum.Ignore &&
            InkDescendants(_matSections).OfType<Control>().All(c => c.MouseFilter == Control.MouseFilterEnum.Ignore),
            "An ink border could intercept card or camera input.");
        for (var i = 0; i < _matTargets.Count; i++)
            for (var j = i + 1; j < _matTargets.Count; j++)
            {
                var a = _matTargets[i]; var b = _matTargets[j];
                var overlap = a.Bounds.Intersection(b.Bounds);
                InkRequire(overlap.Size.X <= .01f || overlap.Size.Y <= .01f,
                    $"Mat sections overlap: {a.Id} {a.Bounds} and {b.Id} {b.Bounds}.");
            }
        foreach (var player in View.Players)
        {
            var holdings = _holdingBounds[player.Id];
            var home = _homeBounds[player.Id];
            var provinces = _matTargets.Where(s => s.Id.StartsWith($"province:{player.Id}:", StringComparison.Ordinal)).Select(s => s.Bounds).FirstOrDefault(new Rect2(_playLeft, 0, 1, 1));
            InkRequire(holdings.End.X + 1 <= Math.Min(home.Position.X, provinces.Position.X),
                $"Seat {player.Id}'s Holdings are not separate on the left of its home/provinces.");
            foreach (var card in player.Home.Where(c => c.AttachedTo is null))
            {
                var tile = _tiles.Single(t => t.Card.Id == card.Id);
                // Expanded units use their one real set of card tiles in the
                // inspection tray. The mat retains a resting placeholder, not
                // duplicate faces. Its tray carries the authoritative owner.
                if (_expandedUnits.Contains(card.Id) && _unitDetail.IsAncestorOf(tile))
                {
                    Node? tray = tile;
                    while (tray is not null && !tray.HasMeta("home_owner")) tray = tray.GetParent();
                    InkRequire(tray is not null && tray.GetMeta("home_owner").AsInt32() == card.OwnerId,
                        $"Expanded Home root {card.Id} lost its owner metadata.");
                    InkRequire(tile.GetParent().HasMeta("unit_id") && tile.GetParent().GetMeta("unit_id").AsString() == card.Id,
                        $"Expanded Home root {card.Id} no longer identifies its own unit.");
                    continue;
                }
                Node? ancestor = tile;
                while (ancestor is not null && !ancestor.HasMeta("mat_section")) ancestor = ancestor.GetParent();
                var expected = $"{(card.Definition?.Type == CardType.Holding ? "holdings" : "home")}:{player.Id}";
                InkRequire(ancestor is not null && ancestor.GetMeta("mat_section").AsString() == expected,
                    $"Home root {card.Id} is not inside its {expected} section.");
            }
        }
        // Reuse the capture contract's complete live membership, including the
        // real attachment tiles. It catches dropped or duplicated roots as well
        // as cards stranded by moving Holdings out of their previous home row.
        var expectedCards = View.Players.SelectMany(p => p.Home.Concat(p.Army).Append(p.Stronghold)
            .Concat(p.Provinces.Where(q => q.Card is not null).Select(q => q.Card!)))
            .Concat(Self.Hand).Select(c => c.Id).Order().ToArray();
        InkRequire(expectedCards.SequenceEqual(_tiles.Select(t => t.Card.Id).Order()),
            "Moving cards into mat sections duplicated or omitted a visible card.");
        foreach (var tile in _tiles.Where(t => t.Card.Zone == Zone.Battlefield))
        {
            Node? lane = tile;
            while (lane is not null && !lane.HasMeta("battle_province")) lane = lane.GetParent();
            InkRequire(lane is not null && lane.GetMeta("battle_province").AsInt32() == tile.Card.ProvinceIndex,
                $"Battlefield card {tile.Card.Id} lost its authoritative province assignment.");
        }
    }

    private void ValidateInkCardClearance()
    {
        var inverse = _world.GetGlobalTransform().AffineInverse();
        foreach (var tile in _tiles.Where(t => _world.IsAncestorOf(t)))
        {
            var card = tile.Card;
            var root = card.AttachedTo is { } parentId ? FindCard(parentId) ?? throw new InvalidOperationException("Attachment has no visible parent.") : card;
            var id = root.Zone switch
            {
                Zone.Province => $"province:{root.OwnerId}:{View.Players.Single(p => p.Id == root.OwnerId).Provinces.Single(p => p.Card?.Id == root.Id).Index}",
                Zone.Battlefield => $"battle:{1 - View.ActivePlayerId}:{root.ProvinceIndex}",
                _ => $"{(root.Definition?.Type is CardType.Holding or CardType.Stronghold ? "holdings" : "home")}:{root.OwnerId}"
            };
            var bounds = _matTargets.Single(s => s.Id == id).Bounds.Grow(-4);
            var corners = tile.FaceCornersInCanvas().Select(p => inverse * p).ToArray();
            InkRequire(corners.All(bounds.HasPoint),
                $"Resting card {card.Id} ({card.Definition?.Name}, bow={card.Bowed}, hover={tile.InteractionScale}) crosses {id} {bounds}; corners={string.Join(';', corners)}.");
        }
        GD.Print("INK_CLEARANCE_PASS: all actual resting tabletop card corners remain inside their own section with padding.");
    }

    private void ValidateInkAttachmentTrays(Dictionary<string, InkMatSection> targets,
        Dictionary<string, Rect2> resting, long version)
    {
        var initiallyExpanded = _expandedUnits.ToArray();
        if (initiallyExpanded.Length > 0) FoldInkProbeTray();
        // Prefer one real root from each represented zone. Do not manufacture
        // attachments or move units to claim Home and Battlefield coverage.
        var candidates = View.Players.SelectMany(player => player.Home.Concat(player.Army)
            .Where(card => card.AttachedTo is null && card.Definition?.Type == CardType.Personality &&
                player.Home.Concat(player.Army).Any(child => child.AttachedTo == card.Id)))
            .GroupBy(card => card.Zone).Select(group => group.First()).ToArray();
        InkRequire(candidates.Length > 0, "Ink attachment regression fixture has no actual attached unit.");
        var exercised = new List<string>();
        foreach (var root in candidates)
        {
            var owner = View.Players.Single(player => player.Id == root.OwnerId);
            var members = owner.Home.Concat(owner.Army).Where(card => card.Id == root.Id || card.AttachedTo == root.Id)
                .Select(card => card.Id).Order().ToArray();
            ExpandInkProbeTray(root.Id);
            InkRequire(_expandedUnits.SetEquals([root.Id]), "Attachment toggle did not expand exactly its chosen unit.");
            AssertInkLayoutUnchanged(targets, resting, "attachment tray expansion");
            ValidateInkBoardTopology();
            var trayTiles = _tiles.Where(tile => _unitDetail.IsAncestorOf(tile)).ToArray();
            InkRequire(members.SequenceEqual(trayTiles.Select(tile => tile.Card.Id).Order()),
                "Expanded tray omitted, duplicated or borrowed a card from another unit.");
            InkRequire(trayTiles.All(tile => tile.Card.OwnerId == root.OwnerId), "Expanded tray mixed card owners.");
            foreach (var tile in trayTiles)
                InkRequire(tile.GetParent().HasMeta("unit_id") && tile.GetParent().GetMeta("unit_id").AsString() == root.Id,
                    $"Expanded attachment {tile.Card.Id} lost its actual parent unit.");
            var attachment = trayTiles.First(tile => tile.Card.AttachedTo == root.Id);
            attachment.EmitSignal(Control.SignalName.MouseEntered); attachment._Process(.2);
            AssertInkLayoutUnchanged(targets, resting, "expanded attachment hover");
            attachment.EmitSignal(Control.SignalName.MouseExited);
            attachment.EmitSignal(Button.SignalName.Pressed);
            InkRequire(_selected?.Id == attachment.Card.Id && _chosen is null,
                "An expanded attachment could not be inspected without starting a command.");
            AssertInkLayoutUnchanged(targets, resting, "expanded attachment inspection");
            DeselectCard();
            FoldInkProbeTray();
            InkRequire(_expandedUnits.Count == 0 && !_tiles.Any(tile => _unitDetail.IsAncestorOf(tile)),
                "Fold left card tiles in the attachment tray.");
            AssertInkLayoutUnchanged(targets, resting, "attachment tray folding");
            ValidateInkBoardTopology();
            var folded = _tiles.Single(tile => tile.Card.Id == root.Id);
            InkRequire(_world.IsAncestorOf(folded), "Fold did not return the actual root card to the table.");
            InkRequire(members.All(id => _tiles.Single(tile => tile.Card.Id == id).GetParent() == folded.GetParent()),
                "Fold did not restore attachments behind their actual parent.");
            InkRequire(View.Version == version, "Attachment expansion, inspection or folding changed the rules state.");
            exercised.Add($"{root.Zone}:{members.Length - 1} attachment(s)");
        }
        // Preserve the requested still's initial open/folded presentation.
        if (initiallyExpanded.FirstOrDefault() is { } restore) ExpandInkProbeTray(restore);
        AssertInkLayoutUnchanged(targets, resting, "restored tray presentation");
        ValidateInkBoardTopology();
        GD.Print($"INK_TRAY_PASS: real card signals expand, inspect and fold {string.Join("; ", exercised)}; resting section footprints, exact membership, parent/owner metadata and match version preserved. Only listed live zones were exercised.");
    }

    private void ExpandInkProbeTray(string rootId)
    {
        var tile = _tiles.Single(t => t.Card.Id == rootId);
        var footer = tile.GetParent().GetParent().GetChildren().OfType<HBoxContainer>().Single();
        footer.GetChildren().OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
    }

    private void FoldInkProbeTray()
    {
        InkDescendants(_unitDetail).OfType<Button>().Single(button => button.Text.StartsWith("Fold unit", StringComparison.Ordinal))
            .EmitSignal(Button.SignalName.Pressed);
    }

    private async Task ValidateMatEmblems()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var version = View.Version;
        var icons = _matSections.IconHotspots;
        InkRequire(icons.Count == _matTargets.Count && icons.All(c => c.MouseFilter == Control.MouseFilterEnum.Ignore),
            "Mat icon identities are missing or their hit regions capture input.");
        foreach (var icon in icons.Where(c => c.Visible))
        {
            var point = icon.GetGlobalRect().GetCenter();
            InkRequire(_matSections.IconAt(point) == icon && icon.HasMeta("flat_tooltip"), "An icon has no discoverable hover name.");
            // Compare in the same screen coordinate space; no icon should cover a
            // resting card, even at a dense overview with many attachments.
            var bounds = icon.GetGlobalRect();
            foreach (var tile in _tiles.Where(t => _world.IsAncestorOf(t)))
                InkRequire(Geometry2D.IntersectPolygons(tile.FaceCornersOnViewport(), new Vector2[] { bounds.Position, new(bounds.End.X, bounds.Position.Y), bounds.End, new(bounds.Position.X, bounds.End.Y) }).Count == 0,
                    "A mat emblem covers a resting card.");
        }
        GetWindow().GrabFocus();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var savedCenter = _cameraCenter; var savedZoom = _cameraZoom; var savedFit = _cameraFitted;
        if (!icons.Any(c => c.Visible && PointerOnTable(c.GetGlobalRect().GetCenter())))
        {
            // A close camera can legitimately put all border-mounted icons offscreen.
            ResetTableCamera();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var visible = icons.FirstOrDefault(c => c.Visible && PointerOnTable(c.GetGlobalRect().GetCenter()));
        InkRequire(visible is not null, "The icon fixture has no visible hotspot to test.");
        HideTableTooltip(); ReadingTooltip.LastText = null;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = visible!.GetGlobalRect().GetCenter() });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ProcessTableTooltip(.6);
        InkRequire(ReadingTooltip.LastText == visible.GetMeta("flat_tooltip").AsString(),
            "Native pointer hover did not route the icon's accessible name through the reading tooltip.");
        HideTableTooltip();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(8, 140) });
        _cameraCenter = savedCenter; _cameraZoom = savedZoom; _cameraFitted = savedFit; ApplyTableCamera();
        foreach (var player in View.Players)
        foreach (var province in player.Provinces.Where(p => !p.Destroyed))
        {
            var id = $"province:{player.Id}:{province.Index}";
            var descriptor = _matTargets.Single(s => s.Id == id);
            var threshold = ProvinceThresholdArt.ThresholdBounds(descriptor.Bounds);
            InkRequire(threshold.Position.Y > ProvinceRowY(player) + PlaneHeight(CardHeight),
                "A fortress threshold overlaps its province card slot.");
            InkRequire(descriptor.ProvinceNumber == province.Index + 1, "A province threshold lost its authoritative public identity.");
            if (province.Card is null) continue;
            var card = _tiles.Single(t => t.Card.Id == province.Card.Id);
            var before = _matSections.CurrentBounds(id);
            card.EmitSignal(Control.SignalName.MouseEntered); card._Process(.2);
            InkRequire(before == _matSections.CurrentBounds(id), "Hovering a province moved its threshold.");
            card.EmitSignal(Control.SignalName.MouseExited); card._Process(.2);
        }
        var first = _matTargets.First(s => s.ProvinceNumber is not null);
        var reveal = _matSections.RevealProgress(first.Id);
        var geometry = _matSections.CurrentBounds(first.Id);
        _matSections.Emphasize(first.Id);
        AdvanceInk(_matSections, .45f);
        InkRequire(_matSections.EmphasisProgress(first.Id) == 1 && _matSections.RevealProgress(first.Id) == reveal && _matSections.CurrentBounds(first.Id) == geometry,
            "Province selection restarted ink or moved its threshold.");
        _matSections.Emphasize(null); AdvanceInk(_matSections, .45f);
        InkRequire(_matSections.EmphasisProgress(first.Id) == 0, "Deselecting a province left its glow active.");
        InkRequire(View.Version == version, "Hovering a mat icon or province seal changed gameplay state.");
        GD.Print("INK_EMBLEM_PASS: native pointer routes icon hover names; icon hit regions pass through input; province seals sit below resting slots and stay fixed during card hover.");
    }

    private void ValidateEmptyProvinceThreshold()
    {
        // Native presentation lifecycle with no fabricated game snapshot: an empty
        // surviving Province 3 retains its frame; removing Province 1 never moves it.
        var renderer = new InkMatSections(); _world.AddChild(renderer);
        try
        {
            InkMatSection[] slots = [new("one", "Province 1", new Rect2(0,0,210,330), ProvinceNumber:1),
                new("three", "Province 3", new Rect2(460,0,210,330), ProvinceNumber:3)];
            renderer.Sync(slots, true, "isolated-empty");
            InkRequire(renderer.Targets.Single(s => s.Id == "three").ProvinceNumber == 3 && renderer.RevealProgress("three") == 1,
                "An empty province did not retain its visible identity.");
            var before = renderer.CurrentBounds("three");
            renderer.Sync([slots[1]], true, "isolated-empty");
            InkRequire(renderer.SectionCount == 1 && renderer.CurrentBounds("three") == before && renderer.Targets[0].ProvinceNumber == 3,
                "Removing another province moved or renumbered the empty survivor.");
            renderer.Sync([], true, "isolated-empty");
            InkRequire(renderer.SectionCount == 0, "Removing a province left an orphan frame or number.");
            GD.Print("INK_PROVINCE_PASS: native empty Province 3 retains identity; removing another slot does not renumber or move it; removal leaves no orphan art. Presentation-only fixture.");
        }
        finally { _world.RemoveChild(renderer); renderer.QueueFree(); }
    }

    private void AssertInkLayoutUnchanged(Dictionary<string, InkMatSection> targets,
        Dictionary<string, Rect2> resting, string action)
    {
        InkRequire(targets.Count == _matTargets.Count && _matTargets.All(s => targets.TryGetValue(s.Id, out var old) && old == s),
            $"Section descriptors changed during {action}.");
        InkRequire(_matSections.Targets.Count == targets.Count &&
            _matSections.Targets.All(s => targets.TryGetValue(s.Id, out var old) && old == s),
            $"Renderer targets changed during {action}.");
        foreach (var (id, bounds) in resting)
            InkRequire(InkSame(bounds, InkBounds(_matSections, id)), $"Section {id} moved during {action}.");
        InkRequire(!_matSections.IsAnimating, $"Settled ink restarted during {action}.");
    }

    private void ValidateInkTransitions()
    {
        var probe = new InkMatSections { Visible = false };
        var uninterrupted = new InkMatSections { Visible = false };
        _world.AddChild(probe); _world.AddChild(uninterrupted);
        try
        {
            var compact = new InkMatSection("growing", "Holdings", new Rect2(80, 80, 320, 220));
            var expanded = compact with { Bounds = new Rect2(55, 60, 600, 440) };
            var contracted = compact with { Bounds = new Rect2(95, 90, 260, 190) };
            var stable = new InkMatSection("stable", "Home", new Rect2(780, 80, 320, 220));
            SyncInk(probe, [compact, stable]); SyncInk(uninterrupted, [compact, stable]);
            InkRequire(!probe.IsAnimating && probe.SectionCount == 2, "Initial mat did not establish stable identities.");
            var cardsSettled = false;
            probe.MotionSettled = uninterrupted.MotionSettled = () => cardsSettled;
            SyncInk(probe, [expanded, stable]); SyncInk(uninterrupted, [expanded, stable]);
            InkRequire(probe.IsAnimating && probe.RevealProgress(compact.Id) == 0, "New border became visible before card movement.");
            AdvanceInk(probe, 2); AdvanceInk(uninterrupted, 2);
            InkRequire(probe.WaitingForCards(compact.Id) && probe.RevealProgress(compact.Id) == 0,
                "Ink revealed on a timer while cards were still moving.");
            InkRequire(InkSame(InkBounds(probe, stable.Id), stable.Bounds), "An unrelated section moved during expansion.");
            SyncInk(probe, [expanded, stable]);
            cardsSettled = true;
            AdvanceInk(probe, .1); AdvanceInk(uninterrupted, .1);
            InkRequire(probe.RevealProgress(compact.Id) == 0, "Ink omitted the quiet pause after cards settled.");
            AdvanceInk(probe, .4); AdvanceInk(uninterrupted, .4);
            InkRequire(probe.RevealProgress(compact.Id) > 0 && probe.RevealProgress(compact.Id) < 1,
                "Post-settlement ink did not have a gradual reveal.");
            SyncInk(probe, [expanded, stable]);
            AdvanceInk(probe, .1); AdvanceInk(uninterrupted, .1);
            InkRequire(Math.Abs(probe.RevealProgress(compact.Id)-uninterrupted.RevealProgress(compact.Id)) < .001f,
                "Unchanged Sync restarted the ink reveal.");

            // A second movement while ink is spreading cancels the obsolete
            // target, dissolves its partial pigment, and waits for actual rest.
            cardsSettled = false;
            SyncInk(probe, [contracted, stable]); AdvanceInk(probe, 1);
            InkRequire(probe.WaitingForCards(compact.Id) && probe.RevealProgress(compact.Id) == 0,
                "Rapid retarget painted a contracted box before movement ended.");
            cardsSettled = true; AdvanceInk(probe, 1.8);
            InkRequire(!probe.IsAnimating && InkSame(InkBounds(probe, compact.Id), contracted.Bounds), "Contraction did not settle at its new bounds.");

            SyncInk(probe, [stable]);
            InkRequire(probe.IsAnimating && probe.SectionCount == 2 && probe.Targets.Count == 1 && probe.CurrentBounds(compact.Id) is not null,
                "A removed section vanished instead of retaining ink while it dissolves.");
            AdvanceInk(probe, .2); SyncInk(probe, [stable]); AdvanceInk(probe, .2);
            InkRequire(probe.CurrentBounds(compact.Id) is not null, "Removed ink disappeared before its fade could finish.");
            AdvanceInk(probe, .2);
            InkRequire(probe.SectionCount == 1 && probe.CurrentBounds(compact.Id) is null && !probe.IsAnimating,
                "Removed ink did not finish, or repeated Sync reset its dissolution.");

            SyncInk(probe, [compact, stable]);
            InkRequire(probe.IsAnimating && probe.SectionCount == 2, "A new section did not enter through a reveal.");
            AdvanceInk(probe, 1.8);
            SyncInk(probe, [expanded with { Active = true }, stable]); AdvanceInk(probe, .1);
            SyncInk(probe, [expanded with { Active = true }, stable], reducedMotion: true);
            InkRequire(probe.ReducedMotion && !probe.IsAnimating && InkSame(InkBounds(probe, compact.Id), expanded.Bounds),
                "Enabling reduced motion did not immediately finish geometry, reveal and active feedback.");
            SyncInk(probe, [stable], reducedMotion: true);
            InkRequire(probe.SectionCount == 1 && !probe.IsAnimating, "Reduced motion still animates a removed section.");

            SyncInk(probe, [compact, stable]);
            cardsSettled = false;
            SyncInk(probe, [expanded, stable]); AdvanceInk(probe, .1);
            SyncInk(probe, [expanded, stable], reducedMotion: true);
            InkRequire(!probe.IsAnimating && probe.RevealProgress(compact.Id) == 1,
                "Reduced motion remained blocked on card animation.");

            var fresh = new InkMatSection("fresh-match", "Provinces", new Rect2(160, 160, 800, 240));
            SyncInk(probe, [fresh], matchKey: "different-match");
            InkRequire(probe.SectionCount == 1 && probe.CurrentBounds(stable.Id) is null &&
                InkSame(InkBounds(probe, fresh.Id), fresh.Bounds) && !probe.IsAnimating,
                "New-match reset retained an old section or its transition.");
        }
        finally
        {
            _world.RemoveChild(probe); probe.QueueFree();
            _world.RemoveChild(uninterrupted); uninterrupted.QueueFree();
        }
    }

    private static void SyncInk(InkMatSections renderer, IReadOnlyList<InkMatSection> descriptors,
        bool reducedMotion = false, string matchKey = "isolated-ink-probe")
    {
        renderer.Sync(descriptors, reducedMotion, matchKey); renderer.SetProcess(false);
    }

    private static void AdvanceInk(InkMatSections renderer, double seconds)
    {
        // The renderer caps a single frame's elapsed time at .1; emulate frames
        // explicitly instead of claiming one large manual tick is elapsed time.
        var remaining = seconds;
        while (remaining > .000001)
        {
            var step = Math.Min(.05, remaining);
            renderer._Process(step); renderer.SetProcess(false); remaining -= step;
        }
    }

    private static Rect2 InkBounds(InkMatSections renderer, string id) => renderer.CurrentBounds(id)
        ?? throw new InvalidOperationException($"Missing ink section {id}.");
    private static bool InkSame(Rect2 a, Rect2 b) => a.Position.DistanceTo(b.Position) < .02f && a.Size.DistanceTo(b.Size) < .02f;
    private static IEnumerable<Node> InkDescendants(Node parent) => parent.GetChildren()
        .SelectMany(child => new[] { child }.Concat(InkDescendants(child)));
    private static void InkRequire(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
