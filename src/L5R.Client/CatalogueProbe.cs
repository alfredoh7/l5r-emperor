using Godot;
namespace L5R.Client;
public partial class CatalogueProbe : Node
{
    private CatalogueScreen _screen=null!;
    private string _output="";
    public override async void _Ready()
    {
        try
        {
            _output=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--out="))?[6..]??ProjectSettings.GlobalizePath("res://../../artifacts/catalogue-checks");Directory.CreateDirectory(_output);
            _screen=new CatalogueScreen();AddChild(_screen);_screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Wait(2);Check(_screen.Scans.PackAvailable,"complete local artwork pack present");
            Check(_screen.Data.Cards.SelectMany(c=>c.Printings).All(_screen.Scans.HasArtwork),"every printing including pinned game faces has WebP artwork");
            Check(_screen.Data.Cards.Length==2149,"full reference catalogue");
            Check(_screen.Data.Cards.All(c=>c.Printings.Length>0),"every card has indexed scan variants");
            Check(_screen.Data.Filter(playable:true).All(c=>c.Playable),"support comes from Core");
            Check(_screen.Data.Filter(clan:"Crane",type:"Personality").All(c=>c.Clans.Contains("Crane")&&c.Types.Contains("Personality")),"combined clan and type filters");
            Check(_screen.Data.Filter(family:"Fate",keyword:"Battle").All(c=>c.DeckTypes.Contains("Fate")&&c.Keywords.Contains("Battle")),"family and keyword filters");
            Check(_screen.Data.Filter(search:"nonesuchzzzz").Length==0,"no results");
            // Every image is local, including on first use. Allow asynchronous decode to finish.
            await Wait(4);await Capture("gallery");
            Click(FindButton(b=>b.Text.Contains("Playable now")));await Wait(.3);Check(_screen.Grid.Cards.Length==_screen.Data.Filter(playable:true).Length&&_screen.Grid.Cards.Length>0,"native playable filter");
            Click(FindButton(b=>b.TooltipText=="Crane cards"));await Wait(.3);Check(_screen.Grid.Cards.Length>0&&_screen.Grid.Cards.All(c=>c.Playable&&c.Clans.Contains("Crane")),"native clan filter combines with support");
            _screen.ResetFilters();await Wait(.2);Click(FindButton(b=>b.Text=="Fate"));await Wait(.3);Check(_screen.Grid.Cards.Length>0&&_screen.Grid.Cards.All(c=>c.DeckTypes.Contains("Fate")),"native family filter");
            _screen.ResetFilters();await Wait(.2);
            Search("Akodo");await Wait(3);Check(_screen.Grid.Cards.All(c=>c.SearchText.Contains("Akodo",StringComparison.OrdinalIgnoreCase)),"live search");
            var tile=_screen.Grid.Tile("oracle-l5r-229")??throw new Exception("Akodo Kisho tile absent");_screen.Grid.EnsureControlVisible(tile);await Wait(.5);Click(tile);await Wait(1);
            Check(_screen.SelectedId=="oracle-l5r-229","native click opens details");Check(_screen.Grid.GetGlobalRect().End.X<=_screen.InspectorRect.Position.X,"inspector cannot cover gallery cards");await Capture("inspection");
            tile=_screen.Grid.Tile("oracle-l5r-229")??throw new Exception("Selected tile absent after reflow");_screen.Grid.EnsureControlVisible(tile);await Wait(.3);Click(tile);await Wait(.5);Check(_screen.SelectedId=="","repeat click dismisses details");
            _screen.ResetFilters();await Wait(.3);_screen.Grid.ScrollVertical=4800;await Wait(1);Check(_screen.Grid.VisibleTileCount<55,"bounded visible tile pool");
            var before=_screen.Grid.ScrollVertical;var other=_screen.Grid.Cards.First();_screen.SelectCard(other);await Wait(.2);PressKey(Godot.Key.Escape);await Wait(.3);Check(_screen.SelectedId=="","Escape dismisses inspector");
            Check(Math.Abs(_screen.Grid.ScrollVertical-before)<=2,"inspection restores exact scrolling position");
            Search("nonesuchzzzz");await Wait(.3);await Capture("empty");
            _screen.ResetFilters();Search("Akodo");await Wait(.5);_screen.SelectCard(_screen.Data.Cards.First(c=>c.Id=="oracle-l5r-229"));await Wait(.3);
            var window=GetWindow();window.Size=new Vector2I(1280,800);await Wait(.8);await Capture("compact");
            Click(FindButton(b=>b.Text.StartsWith("Rules text")));await Wait(.3);await Capture("rules");
            PressKey(Godot.Key.Escape);await Wait(.3);Click(FindButton(b=>b.Text=="More filters"));await Wait(.3);await Capture("advanced");PressKey(Godot.Key.Escape);await Wait(.3);
            var card=_screen.Data.Cards.First(c=>!c.Playable);var fallback=new CatalogueData.Printing {Id="probe-missing",SourceUrl="https://images.oracleofthevoid.com/l5r/missing.jpg",Sha256=new string('0',64)};_screen.Scans.Get(fallback);await Wait(.3);Check(_screen.Scans.State(fallback).Contains("unavailable"),"missing offline scan is explicit");
            var saved=card.Printings;card.Printings=[fallback];_screen.ResetFilters();Search(card.Name);await Wait(.5);await Capture("missing-scan");card.Printings=saved;
            GD.Print("PLAYABLE COUNT "+_screen.Data.Filter(playable:true).Length);
            Check(_screen.Scans.TextureCount<=80,"bounded texture cache");
            Check(_screen.Scans.LoadedFromPackCount>30,"gallery and inspector load WebP from local pack");
            var pinned=_screen.Data.Cards.SelectMany(c=>c.Printings).First(p=>!string.IsNullOrEmpty(p.ResourcePath));
            var oldBytes=Godot.FileAccess.GetFileAsBytes(pinned.ResourcePath);
            var oldHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(oldBytes)).ToLowerInvariant();
            var migrationDir=ProjectSettings.GlobalizePath("user://catalogue-migration-probe");Directory.CreateDirectory(migrationDir);
            var oldFile=Path.Combine(migrationDir,oldHash+".jpg");File.WriteAllBytes(oldFile,oldBytes);
            var untouched=Path.Combine(migrationDir,"unrelated.jpg");File.WriteAllText(untouched,"preserve");
            var migration=new CatalogueScans(legacyCacheDirectory:migrationDir);migration.Get(pinned);await Wait(.5);
            Check(migration.TextureCount==1&&!File.Exists(oldFile)&&File.Exists(untouched),"verified WebP retires only matching legacy JPEG cache");migration.Close();
            File.WriteAllText(oldFile,"not the recorded original");migration=new CatalogueScans(legacyCacheDirectory:migrationDir);migration.Get(pinned);await Wait(.5);
            Check(migration.TextureCount==1&&File.Exists(oldFile),"migration preserves files that do not match source receipt");migration.Close();File.Delete(oldFile);File.Delete(untouched);Directory.Delete(migrationDir);
            // A present ZIP with a damaged member must not produce a texture or fetch a JPEG.
            var original=_screen.Data.Cards.SelectMany(c=>c.Printings).First(p=>string.IsNullOrEmpty(p.ResourcePath));
            var corruptPath="user://catalogue-corrupt-probe.zip";
            using(var writer=new ZipPacker()) { Check(writer.Open(corruptPath)==Error.Ok,"corrupt fixture created");writer.StartFile(original.Sha256+".webp");writer.WriteFile(new byte[]{1,2,3});writer.CloseFile();writer.Close(); }
            var corrupt=new CatalogueScans(corruptPath);corrupt.Get(original);await Wait(.4);
            Check(corrupt.TextureCount==0&&corrupt.State(original).Contains("unavailable"),"corrupt WebP rejected without an HTTP fallback");corrupt.Close();DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(corruptPath));
            _screen.QueueFree();await Wait(.3);GD.Print("CATALOGUE PASS: data, native select/deselect, filters, virtual rows, Escape, compact layout, missing scans, texture bound");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name){_screen.QueueRedraw();await Wait(.1);RenderingServer.ForceDraw();GD.Print("CAPTURE "+name);GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output,name+".png"));}
    private void Search(string text) { _screen.Search.GrabFocus(); _screen.Search.SelectAll(); PressKey(Godot.Key.Backspace); foreach(var ch in text) { Input.ParseInputEvent(new InputEventKey { Unicode=ch, Keycode=(Godot.Key)char.ToUpperInvariant(ch), Pressed=true }); Input.ParseInputEvent(new InputEventKey { Unicode=ch, Keycode=(Godot.Key)char.ToUpperInvariant(ch), Pressed=false }); } }
    private static IEnumerable<Node> Walk(Node root) { foreach(var child in root.GetChildren()) { yield return child; foreach(var n in Walk(child)) yield return n; } }
    private Button FindButton(Func<Button,bool> predicate)=>Walk(_screen).OfType<Button>().First(predicate);
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);GD.Print("PASS "+message);}
    private static void Click(Control c){var p=c.GetGlobalRect().GetCenter();Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p});Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});}
    private static void PressKey(Godot.Key key){Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=true});Input.ParseInputEvent(new InputEventKey{Keycode=key,Pressed=false});}
}
