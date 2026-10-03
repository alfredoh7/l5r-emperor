using Godot;

namespace L5R.Client;

public partial class Main
{
    private async Task CheckProvinceReview(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            async Task Capture(string name)
            {
                MoveProbeMouse(new Vector2(8, GetViewport().GetVisibleRect().Size.Y / 2)); HideTableTooltip();
                await CoverageFrames();
                using var image = GetViewport().GetTexture().GetImage();
                RequireCamera(image.SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "province capture failed");
            }
            await CoverageFrames(); await CoverageFocus();
            _reducedMotion = true; PrepareProvinceReview(); await CoverageFrames();
            await Capture("00-initial");
            foreach (var id in new[] { "preview-0-1-a", "preview-0-1-b", "preview-region-0-1" })
            {
                await CoverageFocus();
                var tile = _tiles.Single(t => t.Card.Id == id);
                RequireCamera(tile.Card.Definition is { } definition && CardTile.Artwork(definition) is not null,
                    "full scan missing for " + id);
                var point = ScreenCenter(tile); MoveProbeMouse(point);
                CameraMouse(MouseButton.Left, true, point); CameraMouse(MouseButton.Left, false, point);
                RequireCamera(_selected?.Id == id, $"independent province selection: {id}; selected={_selected?.Id}, point={point}, pointer={PointerOnTable(point)}, blocked={CameraBlocked}, focus={GetWindow().HasFocus()}, hovered={TableHovered?.Name}");
                await CoverageFrames();
            }
            DeselectCard(); await Capture("01-two-cards");
            _provinceReviewScenario = 1; PrepareProvinceReview(); await CoverageFrames();
            var hidden = _tiles.Single(t => t.Card.Id == "hidden-p0-province1-slot1");
            await CoverageFocus();
            var hiddenPoint = ScreenCenter(hidden); MoveProbeMouse(hiddenPoint);
            CameraMouse(MouseButton.Left, true, hiddenPoint); CameraMouse(MouseButton.Left, false, hiddenPoint);
            RequireCamera(_selected is { Definition: null, FaceUp: false }, "face-down slot inspection disclosed identity");
            await Capture("02-face-down");
            _provinceReviewScenario = 2; PrepareProvinceReview(); await CoverageFrames();
            RequireCamera(_provinceOrderConfirm!.Disabled, "order preselected without input");
            var staleId = View.PendingProvinceOrder!.Id; var staleVersion = View.Version;
            if (Self.Hand.FirstOrDefault() is { } handCard) SelectCard(handCard);
            var firstChoice = HudDescendants(_provinceOrderTray!).OfType<CardTile>().Single(t => t.Card.Id == "hidden-p0-province1-slot1");
            await CoverageFocus();
            var choicePoint = firstChoice.GetGlobalRect().GetCenter(); MoveProbeMouse(choicePoint);
            CameraMouse(MouseButton.Left, true, choicePoint); CameraMouse(MouseButton.Left, false, choicePoint);
            await CoverageFrames();
            RequireCamera(_provinceOrderSelection == "b-a", "native slot choice was blocked by hand or board input");
            await Capture("03-order-tray");
            // Exercise the production layout path without the fixture toolbar guard.
            var reviewBar = _provinceReviewBar; _provinceReviewBar = null;
            GetWindow().Size = new Vector2I(1440, 900); await CoverageFrames();
            RequireCamera(_provinceOrderTray!.Position.X >= 144 && _provinceOrderTray.GetRect().End.X < _layoutWidth - SidebarWidth,
                "resizing a live tray obscured its confirmation behind the sidebar");
            _provinceReviewBar = reviewBar;
            GetWindow().Size = new Vector2I(1920, 1080); await CoverageFrames();
            RequireCamera(!_provinceOrderConfirm!.Disabled, "valid order cannot be confirmed");
            await CoverageFocus();
            var confirmPoint = _provinceOrderConfirm.GetGlobalRect().GetCenter(); MoveProbeMouse(confirmPoint);
            CameraMouse(MouseButton.Left, true, confirmPoint); CameraMouse(MouseButton.Left, false, confirmPoint);
            await CoverageFrames();
            RequireCamera(_provinceReviewConfirmed == "b-a" && View.PendingProvinceOrder is null && _provinceOrderTray is null, "order did not submit exact selected option and retire tray");
            PrepareProvinceReview(); ConfirmProvinceOrder(staleId, staleVersion, "a-b");
            RequireCamera(View.PendingProvinceOrder is not null && _provinceReviewConfirmed is null, "stale confirmation changed new decision");
            _provinceReviewSeat = 1; PrepareProvinceReview(); await Capture("04-opponent-waiting");
            RequireCamera(_provinceOrderTray is null && View.PendingProvinceOrder!.Options.Length == 0, "opponent received decision choices");
            _provinceReviewScenario = 4; PrepareProvinceReview(); await Capture("05-empty-slot");
            RequireCamera(ProvincePresentation.Slots(Self.Provinces[1]).Length == 2, "empty slot lost its position");
            _provinceReviewScenario = 5; PrepareProvinceReview(); ResetTableCamera(); await Capture("06-crowded");
            RequireCamera(_matTargets.Count(s => s.ProvinceNumber is not null) == 8, "two slots produced extra provinces");
            _provinceReviewSeat = 0; _provinceReviewScenario = 0; PrepareProvinceReview();
            GetWindow().Size = new Vector2I(1440, 900); await CoverageFrames(); FocusProvinceReview(); await Capture("07-1440x900");
            GetWindow().Size = new Vector2I(2560, 1080); await CoverageFrames(); FocusProvinceReview(); await Capture("08-ultrawide");
            GD.Print("PROVINCE_REVIEW_PASS: native independent card/Region clicks; hidden inspection; order confirmation; stale rejection; other seat; empty/crowded; 1440x900 and ultrawide.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
