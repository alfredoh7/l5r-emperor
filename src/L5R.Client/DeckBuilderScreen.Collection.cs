using Godot;
namespace L5R.Client;

public partial class DeckBuilderScreen
{
    public sealed record MissingCard(CatalogueData.Card Card, int InDeck, int Owned, int Need);
    public MissingCard[] MissingCards() => Draft.Cards.Select(info => new MissingCard(_byId[info.Id], Draft.Count(info.Id), MyCollection.Store.Count(info.Id),
        Math.Max(0, Draft.Count(info.Id) - MyCollection.Store.Count(info.Id)))).Where(c => c.Need > 0).OrderBy(c => c.Card.Name).ToArray();
    public string MissingCsv() => CollectionCsv.Write(MissingCards().Select(c => new[] { c.Card.Id, MyCollection.Identities(Data).Single(i => i.Id == c.Card.Id).VersionName, c.Need.ToString(System.Globalization.CultureInfo.InvariantCulture) })).Replace("card_id,card_name,owned_count", "card_id,card_name,needed_count");
    private void ExportMissing()
    {
        var file = new FileDialog { Title = "Export missing cards", FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Filesystem, CurrentFile = "missing-cards.csv", Filters = ["*.csv ; CSV"], Theme = Theme };
        AddChild(file); file.FileSelected += path => { try { File.WriteAllText(path, MissingCsv(), new System.Text.UTF8Encoding(true)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Notice(e.Message); } file.QueueFree(); }; file.Canceled += file.QueueFree; file.PopupCentered(new Vector2I(900, 600));
    }
    private void ShowMissing()
    {
        var panel = BeginOverlay("Missing from this deck");
        Put(panel, CatalogueArt.Label("Ownership does not block drafting. These are physical copies to complete your deck.", 18), 30, 75, _w - 60, 34);
        Put(panel, CatalogueArt.Button("Export missing list", ExportMissing), _w - 280, 18, 200, 40);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; Put(panel, scroll, 30, 128, _w - 60, _h - 175);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 14); scroll.AddChild(box);
        foreach (var entry in MissingCards())
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 20); box.AddChild(row);
            var face = new DeckFace(entry.Card, Scans) { CustomMinimumSize = new Vector2(82, 115) }; row.AddChild(face); face.Pressed += () => ShowComparison([entry.Card]);
            var label = CatalogueArt.Label($"{entry.Card.Name} · Need {entry.Need}\nIn deck: {entry.InDeck} · Owned: {entry.Owned}", 22); label.CustomMinimumSize = new Vector2(600, 100); row.AddChild(label);
        }
        if (MissingCards().Length == 0) box.AddChild(CatalogueArt.Label("Your collection covers this deck.", 23));
    }
}

