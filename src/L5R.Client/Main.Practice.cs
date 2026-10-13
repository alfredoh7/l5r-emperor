using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class Main
{
    private CanvasLayer? _practiceLayer;
    private bool PracticeVisible => _practiceLayer is not null && IsInstanceValid(_practiceLayer);
    private void ClosePracticeSetup()
    {
        _practiceLayer?.QueueFree(); _practiceLayer = null;
        _opponentClock = 0;
        if (!MenuVisible) Render();
    }
    private void ShowPracticeSetup()
    {
        if (PracticeVisible) return;
        HideTableTooltip(); ReleaseTableFocus(); _cameraDragging = false;
        _practiceLayer = new CanvasLayer { Layer = 90 }; AddChild(_practiceLayer);
        var screen = new PracticeSetupScreen(); _practiceLayer.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.Closed += ClosePracticeSetup;
        screen.StartMatch = (your, opponent) =>
        {
            void Begin()
            {
                try
                {
                    // Core validates before the active session is disconnected.
                    _session.StartCustomPractice(your, opponent); _session.AllowIdleBattlePasses();
                    _turnBloomGate.Reset(); _turnBloom.Cancel(); ResetStatHistory();
                    _presentationSnapshot = null; _battlePresentationStage = 0; _motionView = null; _cameraGlide = false;
                    _enteringPrivate = false; _tableLayoutSeen = false; _cameraFitted = true;
                    _selected = null; _selectedHomeReminder = null; _chosen = null; _expandedUnits.Clear(); _sources.Clear(); _assignments.Clear();
                    _autoOpponent = true; ClosePracticeSetup(); EnterTable();
                }
                catch (ArgumentException e) { var (dialog, body) = Dialog("Deck needs attention", 660, 380); body.AddChild(Palette.Wrapped(e.Message, 22)); }
            }
            if (_session.Online || _session.Snapshot is not null)
                Confirm("Start a new Practice?", _session.Online ? "Leave your private table and start Practice? This disconnects your seat; it does not concede." : "Replace the current Practice? Export a replay first if you want to keep it.", Begin);
            else Begin();
        };
    }
}
