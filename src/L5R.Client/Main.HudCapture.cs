using Godot;
namespace L5R.Client;

public partial class Main
{
    private bool _hudProbeDone;
    private async void ProbeHudIcons()
    {
        try
        {
            for(var i=0;i<40&&!GetWindow().HasFocus();i++) { GetWindow().GrabFocus(); await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout); }
            if(!GetWindow().HasFocus()) throw new Exception("HUD native test window did not gain focus");
            var version=View.Version;
            var buttons=_menu.GetChildren().OfType<TableIconButton>().ToArray();
            if(buttons.Length!=4 || buttons.Any(b=>b.Text!="" || b.TooltipText=="")) throw new Exception("Table icons lost tooltip names or retained labels");
            foreach(var b in buttons.Where(b=>b.Visible))
                if(!GetViewport().GetVisibleRect().Encloses(b.GetGlobalRect())) throw new Exception("Toolbar icon outside viewport");
            void Click(TableGlyph glyph)
            {
                var point=buttons.Single(b=>b.Glyph==glyph).GetGlobalRect().GetCenter();
                MoveProbeMouse(point); CameraMouse(MouseButton.Left,true,point); CameraMouse(MouseButton.Left,false,point);
            }
            ZoomTable(1.4f,_tableViewport.Size/2);
            Click(TableGlyph.Fit); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(!_cameraFitted || !Mathf.IsEqualApprox(_cameraZoom,FitZoom)) throw new Exception("Fit icon did not reset table camera");
            var cameraCenter=_cameraCenter;
            CameraKey(Key.Right,true); await ToSignal(GetTree().CreateTimer(.25),SceneTreeTimer.SignalName.Timeout);CameraKey(Key.Right,false);
            if(_cameraCenter.X<=cameraCenter.X)throw new Exception("Fit icon trapped keyboard focus and prevented arrow panning");
            Click(TableGlyph.Fit);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            foreach(var glyph in new[]{TableGlyph.Menu,TableGlyph.Rules})
            {
                Click(glyph); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var dialog=GetChildren().OfType<AcceptDialog>().LastOrDefault(d=>d.Visible&&!d.IsQueuedForDeletion());
                if(dialog is null) throw new Exception($"Native {glyph} icon click did not open its dialog");
                dialog.Hide(); dialog.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            var pile=TablePiles.Where(p=>p.Count>0).MaxBy(p=>p.Count)!;
            var face=pile.TopFace;
            var corner=face.Position+new Vector2(face.Size.X/2,2);
            var target=_tableSurface.ProjectCanvas(pile.GetGlobalTransform()*corner);
            MoveProbeMouse(target); CameraMouse(MouseButton.Left,true,target);CameraMouse(MouseButton.Left,false,target);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var pileDialog=GetChildren().OfType<AcceptDialog>().LastOrDefault(d=>d.Visible&&!d.IsQueuedForDeletion());
            if(pileDialog is null)throw new Exception("Exposed pile top edge is not clickable");
            pileDialog.Hide();pileDialog.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(View.Version!=version) throw new Exception("HUD tools altered match state");
            ReleaseTableFocus(); MoveProbeMouse(new Vector2(320,140)*_root.Scale);
            _hudProbeDone=true;
            GD.Print("HUD_PASS: native fit/menu/rules clicks; tooltips; responsive bounds; exposed pile top-edge click; no match commands.");
        }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1);_capturePath=null; }
    }
}
