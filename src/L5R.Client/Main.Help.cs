using Godot;

namespace L5R.Client;

public partial class Main
{
    private void ShowRules()
    {
        var (_, body) = Dialog("Table guide · Emperor Edition", 700, 650);
        body.AddChild(Palette.Text("Learn the table", 30, Palette.Gold, title: true));
        body.AddChild(Palette.Wrapped("Hover over a card for its complete face and current state. Hover over phases, province labels and action buttons for context. Your next move always explains the current decision.", 15));
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 440) }; body.AddChild(tabs);
        foreach (var group in new[] { (Name: "Cards & table", Keys: new[] { "bow", "gold", "province", "unit", "force" }), (Name: "Actions & battle", Keys: new[] { "timing", "battle", "honor", "favor", "targets" }) })
        {
            var scroll = new ScrollContainer { Name = group.Name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; tabs.AddChild(scroll);
            var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 14); scroll.AddChild(content);
            foreach (var topic in RulesHelp.Topics.Where(t => group.Keys.Contains(t.Key)))
            {
                content.AddChild(Palette.Text(topic.Title, 21, Palette.Gold, title: true));
                content.AddChild(Palette.Wrapped(topic.Body, 15, Palette.PaperDim));
            }
        }
    }
}
