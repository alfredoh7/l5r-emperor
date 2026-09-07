using Godot;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace L5R.Client;

/// <summary>Offline reference browser. Call LibraryDialog.Show(this) from a scene node.</summary>
public static class LibraryDialog
{
    private const string BundlePath = "res://Content/library.json";
    private static Bundle? _cached;

    public static void Show(Node parent)
    {
        try
        {
            if (_cached is null)
            {
                using var file = Godot.FileAccess.Open(BundlePath, Godot.FileAccess.ModeFlags.Read);
                if (file is null) throw new InvalidOperationException("The reference library is missing from this build.");
                _cached = JsonSerializer.Deserialize<Bundle>(file.GetAsText(), new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                }) ?? throw new InvalidOperationException("The reference library could not be read.");
                if (_cached.SchemaVersion != 1) throw new InvalidOperationException("This reference library uses an unsupported format.");
            }
            new Browser(_cached).Open(parent);
        }
        catch (Exception error)
        {
            GD.PushError($"Library: {error.Message}");
            var dialog = new AcceptDialog
            {
                Title = "Library unavailable", Theme = Palette.MakeTheme(),
                DialogText = "The packaged reference library could not be loaded. Rebuild the content bundle and open the library again.",
                DialogAutowrap = true, Size = new Vector2I(520, 180)
            };
            parent.AddChild(dialog);
            dialog.Confirmed += dialog.QueueFree;
            dialog.Canceled += dialog.QueueFree;
            dialog.PopupCentered();
        }
    }

    private sealed class Browser(Bundle data)
    {
        private readonly Dictionary<string, Card> _cards = data.Cards.ToDictionary(card => card.Id);
        private readonly Dictionary<string, Texture2D> _textures = [];

        public void Open(Node parent)
        {
            var dialog = new Window
            {
                Title = "Reference library", Theme = LibraryTheme(),
                Size = new Vector2I(1280, 790), MinSize = new Vector2I(960, 600),
                ContentScaleMode = Window.ContentScaleModeEnum.Disabled,
                WrapControls = false, Unresizable = false, Exclusive = true, Transient = true
            };
            parent.AddChild(dialog);
            dialog.CloseRequested += dialog.QueueFree;
            dialog.WindowInput += input =>
            {
                if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
                {
                    dialog.SetInputAsHandled();
                    dialog.QueueFree();
                }
            };
            var panel = new PanelContainer { ClipContents = true };
            panel.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Ink, Palette.Gold, 20));
            dialog.AddChild(panel);
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // A plain Control keeps transient text minimum sizes from expanding the
            // window. Header, scrolling workspace, and footer have separate bounds.
            var body = new Control { ClipContents = true };
            panel.AddChild(body);
            var header = new VBoxContainer { ClipContents = true };
            header.AddChild(Palette.Text("The collection", 30, Palette.Paper, true));
            header.AddChild(Palette.Wrapped("Your imported decks, the Emperor reference catalogue, and supplied card scans. These reference decks are not yet playable.", 16, Palette.PaperDim));
            body.AddChild(header);
            header.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
            header.OffsetBottom = 96;
            var workspace = new Control { ClipContents = true };
            body.AddChild(workspace);
            workspace.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            workspace.OffsetTop = 104;
            workspace.OffsetBottom = -64;
            var tabs = new TabContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                UseHiddenTabsForMinSize = false, ClipContents = true
            };
            tabs.AddChild(DeckTab());
            tabs.AddChild(CatalogueTab());
            tabs.AddChild(ArtworkTab());
            workspace.AddChild(tabs);
            tabs.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            void FitTabs() => tabs.Size = workspace.Size;
            workspace.Resized += FitTabs;
            tabs.MinimumSizeChanged += () => Callable.From(FitTabs).CallDeferred();
            var footer = new HBoxContainer();
            body.AddChild(footer);
            footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
            footer.OffsetTop = -44;
            footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var close = Palette.Button("Return to table", dialog.QueueFree);
            close.CustomMinimumSize = new Vector2(180, 44);
            footer.AddChild(close);
            dialog.PopupCenteredClamped(new Vector2I(1280, 790), 0.94f);
        }

        private Control DeckTab()
        {
            var page = Page("Imported decks");
            var layout = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            page.AddChild(layout);
            var navigation = Column(225);
            navigation.AddChild(Palette.Text($"{data.Decks.Length} imported decks", 18, Palette.Paper, true));
            var decks = List();
            foreach (var deck in data.Decks)
            {
                var index = decks.AddItem(deck.Name);
                decks.SetItemTooltip(index, $"{deck.DeclaredEra} source label · {deck.CardCount} supplied cards");
            }
            navigation.AddChild(decks);
            navigation.AddChild(Palette.Wrapped("Era names are labels from the supplied deck files.", 14, Palette.Muted));
            layout.AddChild(navigation);
            var contents = Column(370);
            var title = Palette.Text("", 24, Palette.Paper, true);
            title.ClipText = true;
            var summary = Palette.Wrapped("", 15, Palette.PaperDim);
            var status = Palette.Wrapped("", 14, Palette.Gold);
            contents.AddChild(title);
            contents.AddChild(summary);
            contents.AddChild(status);
            var entries = List();
            contents.AddChild(entries);
            layout.AddChild(contents);
            var detail = DetailColumn();
            layout.AddChild(detail.Scroll);
            Deck? selected = null;

            void SelectEntry(int index)
            {
                if (selected is null || index < 0 || index >= selected.Entries.Length) return;
                var entry = selected.Entries[index];
                if (entry.CardId is not null && _cards.TryGetValue(entry.CardId, out var card))
                    ShowCard(detail.Content, card, $"{entry.Quantity} in {selected.Name}");
                else
                {
                    Palette.Clear(detail.Content);
                    detail.Content.AddChild(Title(entry.Name));
                    detail.Content.AddChild(Palette.Text($"{entry.Quantity} supplied · identity unresolved", 15, Palette.Gold));
                    detail.Content.AddChild(Palette.Wrapped(entry.ResolutionReason, 16, Palette.PaperDim));
                    detail.Content.AddChild(Palette.Wrapped("The saved catalogue covers Emperor. An unmatched name in an Ivory-labelled file may belong to a different era; no identity or edition is inferred.", 16, Palette.PaperDim));
                }
                detail.Scroll.ScrollVertical = 0;
            }

            void SelectDeck(int index)
            {
                selected = data.Decks[index];
                title.Text = selected.Name;
                var partitions = selected.PartitionCounts;
                summary.Text = $"{selected.DeclaredEra} source label · {selected.CardCount} supplied cards\n"
                    + string.Join(" · ", partitions.Select(part => $"{PartitionName(part.Key)} {part.Value}"));
                var resolved = selected.ResolutionCounts.GetValueOrDefault("resolved");
                status.Text = $"{resolved}/{selected.EntryCount} entries identified · legality unverified";
                entries.Clear();
                for (var i = 0; i < selected.Entries.Length; i++)
                {
                    var entry = selected.Entries[i];
                    entries.AddItem($"{entry.Quantity}×  {entry.Name}");
                    entries.SetItemTooltip(i, entry.CardId is null ? "Identity unresolved" : string.Join(" · ", entry.Types));
                    if (entry.CardId is null) entries.SetItemCustomFgColor(i, Palette.Gold);
                }
                if (selected.Entries.Length > 0) { entries.Select(0); SelectEntry(0); }
            }

            decks.ItemSelected += index => SelectDeck((int)index);
            entries.ItemSelected += index => SelectEntry((int)index);
            if (data.Decks.Length > 0) { decks.Select(0); SelectDeck(0); }
            return page;
        }

        private Control CatalogueTab()
        {
            var page = Page("Card catalogue");
            page.AddChild(Palette.Wrapped($"Oracle of the Void · Emperor catalogue membership · saved {data.CatalogueSource.RetrievedOn}. Current text may include later wording or errata; historical legality and game support are unverified.", 15, Palette.PaperDim));
            var search = new LineEdit
            {
                PlaceholderText = "Search names, clans, keywords, or rules text…", ClearButtonEnabled = true,
                CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = "Every search word must appear in the card name or metadata."
            };
            var tools = new HBoxContainer();
            tools.AddChild(search);
            var type = new OptionButton { CustomMinimumSize = new Vector2(180, 44) };
            type.AddItem("All card types");
            foreach (var item in data.Cards.SelectMany(card => card.Types).Distinct().Order()) type.AddItem(item);
            tools.AddChild(type);
            page.AddChild(tools);
            var layout = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            var navigation = Column(445);
            var count = Palette.Text("", 15, Palette.Muted);
            navigation.AddChild(count);
            var results = List();
            navigation.AddChild(results);
            layout.AddChild(navigation);
            var detail = DetailColumn();
            layout.AddChild(detail.Scroll);
            page.AddChild(layout);
            Card[] visible = [];
            var searchable = data.Cards.Select(card => (Card: card,
                Text: string.Join(" ", new[] { card.Name, card.CurrentOracleText }
                    .Concat(card.Clans).Concat(card.Keywords).Concat(card.Types)).ToLowerInvariant())).ToArray();

            void SelectCard(int index)
            {
                if (index < 0 || index >= visible.Length) return;
                ShowCard(detail.Content, visible[index], "Reference metadata");
                detail.Scroll.ScrollVertical = 0;
            }

            void Filter()
            {
                var terms = search.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var cardType = type.Selected == 0 ? null : type.GetItemText(type.Selected);
                visible = searchable.Where(item => (cardType is null || item.Card.Types.Contains(cardType))
                    && terms.All(term => item.Text.Contains(term, StringComparison.Ordinal))).Select(item => item.Card).ToArray();
                results.Clear();
                foreach (var card in visible)
                {
                    var index = results.AddItem(card.Name);
                    results.SetItemTooltip(index, string.Join(" · ", card.Types.Concat(card.Clans)));
                }
                count.Text = $"{visible.Length:N0} of {data.Cards.Length:N0} cards · reference only";
                if (visible.Length > 0) { results.Select(0); SelectCard(0); }
                else
                {
                    Palette.Clear(detail.Content);
                    detail.Content.AddChild(Title("No matching cards"));
                    detail.Content.AddChild(Palette.Wrapped("Try a shorter name, another keyword, or all card types.", 16, Palette.PaperDim));
                }
            }

            results.ItemSelected += index => SelectCard((int)index);
            search.TextChanged += _ => Filter();
            type.ItemSelected += _ => Filter();
            Filter();
            return page;
        }

        private Control ArtworkTab()
        {
            var page = Page("Uploaded artwork");
            var linkedCount = data.Artwork.Count(art => art.MappingStatus == "source_path_match");
            page.AddChild(Palette.Wrapped($"{data.Artwork.Length} supplied scans · {linkedCount} named by image paths in the Sun and Moon database, updated in 2017. These links do not verify the pictured printing or historical legality.", 15, Palette.PaperDim));
            var layout = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            page.AddChild(layout);
            var gallery = Column(440);
            var search = new LineEdit
            {
                PlaceholderText = "Search card names or filenames…", ClearButtonEnabled = true,
                CustomMinimumSize = new Vector2(0, 40)
            };
            gallery.AddChild(search);
            var folders = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
            folders.AddItem("All source folders");
            foreach (var folder in data.Artwork.Select(art => art.SourceFolder).Distinct().Order()) folders.AddItem(folder);
            gallery.AddChild(folders);
            var count = Palette.Text("", 14, Palette.Muted);
            gallery.AddChild(count);
            var images = List();
            images.IconMode = ItemList.IconModeEnum.Top;
            images.FixedIconSize = new Vector2I(94, 132);
            images.FixedColumnWidth = 132;
            images.SameColumnWidth = true;
            images.MaxColumns = 3;
            images.MaxTextLines = 3;
            images.AddThemeConstantOverride("v_separation", 18);
            gallery.AddChild(images);
            layout.AddChild(gallery);
            var preview = DetailColumn();
            layout.AddChild(preview.Scroll);
            Artwork[] visible = [];

            void SelectImage(int index)
            {
                if (index < 0 || index >= visible.Length) return;
                var art = visible[index];
                Palette.Clear(preview.Content);
                var linked = art.MappingStatus == "source_path_match";
                preview.Content.AddChild(Title(linked && !string.IsNullOrWhiteSpace(art.CardName) ? art.CardName : art.Filename));
                preview.Content.AddChild(Palette.Wrapped($"{art.SourceFolder}/{art.Filename} · {art.Width} × {art.Height}", 14, Palette.PaperDim));
                preview.Content.AddChild(Palette.Wrapped(linked
                    ? "Source path match · printing and legality unverified"
                    : art.MappingStatus == "ambiguous" ? "Multiple possible source records · identity unresolved" : "No unique source record linked", 14, Palette.Gold));
                preview.Content.AddChild(new TextureRect
                {
                    Texture = Texture(art.ResourcePath),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(0, 360),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                });
                if (linked)
                {
                    preview.Content.AddChild(new HSeparator());
                    preview.Content.AddChild(Palette.Text("SUN AND MOON SOURCE · UPDATED 2017", 13, Palette.Muted));
                    preview.Content.AddChild(Palette.Wrapped($"Database {art.DatabaseVersion} · {art.CardType}\nSource card ID {art.SourceCardId} · record {art.LegacyCardId}", 14, Palette.PaperDim));
                    preview.Content.AddChild(Palette.Wrapped($"Database image path: {art.SourceImagePath}\nImage edition label: {art.ImageEdition}", 14, Palette.PaperDim));
                    if (art.DeclaredLegalities.Length > 0)
                        preview.Content.AddChild(Palette.Wrapped("Source legality tags (unverified): " + string.Join(" · ", art.DeclaredLegalities), 14, Palette.PaperDim));
                    preview.Content.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(art.MappingReason)
                        ? "The database names this uploaded file in its image path. The scan's printing still requires review."
                        : art.MappingReason, 14, Palette.Muted));
                    preview.Content.AddChild(Palette.Text("SOURCE RULES TEXT", 13, Palette.Muted));
                    preview.Content.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(art.LegacyText)
                        ? "No rules text recorded in this source record." : DisplayOracleText(art.LegacyText), 17));
                    preview.Content.AddChild(Palette.Wrapped("This is the database's wording, not verified Emperor printing text or executable card behavior.", 14, Palette.Muted));
                }
                else if (!string.IsNullOrWhiteSpace(art.MappingReason))
                    preview.Content.AddChild(Palette.Wrapped(art.MappingReason, 15, Palette.PaperDim));
                preview.Scroll.ScrollVertical = 0;
            }

            void Filter()
            {
                var folder = folders.Selected == 0 ? null : folders.GetItemText(folders.Selected);
                var terms = search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                visible = data.Artwork.Where(art => (folder is null || art.SourceFolder == folder)
                    && terms.All(term => $"{art.CardName} {art.Filename} {art.SourceFolder} {art.CardType}".Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
                images.Clear();
                foreach (var art in visible)
                {
                    var linked = art.MappingStatus == "source_path_match" && !string.IsNullOrWhiteSpace(art.CardName);
                    var name = linked ? $"{art.CardName}\n{art.Filename}" : art.Filename;
                    var index = images.AddItem(name, Texture(art.ResourcePath));
                    images.SetItemTooltip(index, $"{name}\n{art.SourceFolder} · {(linked ? "2017 database path match; printing unverified" : "identity unresolved")}");
                }
                count.Text = $"{visible.Length} of {data.Artwork.Length} scans";
                if (visible.Length > 0)
                {
                    images.Select(0); SelectImage(0);
                    Callable.From(images.EnsureCurrentIsVisible).CallDeferred();
                }
                else
                {
                    Palette.Clear(preview.Content);
                    preview.Content.AddChild(Title("No matching scans"));
                    preview.Content.AddChild(Palette.Wrapped("Try a shorter card name or choose all source folders.", 16, Palette.PaperDim));
                }
            }

            page.VisibilityChanged += () =>
            {
                if (page.IsVisibleInTree() && visible.Length > 0)
                    Callable.From(images.EnsureCurrentIsVisible).CallDeferred();
            };
            images.ItemSelected += index => SelectImage((int)index);
            folders.ItemSelected += _ => Filter();
            search.TextChanged += _ => Filter();
            Filter();
            return page;
        }

        private Texture2D? Texture(string path)
        {
            if (_textures.TryGetValue(path, out var existing)) return existing;
            var texture = GD.Load<Texture2D>(path);
            if (texture is not null) _textures[path] = texture;
            return texture;
        }

        private void ShowCard(VBoxContainer detail, Card card, string context)
        {
            Palette.Clear(detail);
            detail.AddChild(Palette.Text(context, 14, Palette.Gold));
            detail.AddChild(Title(card.Name));
            detail.AddChild(Palette.Wrapped(string.Join(" · ", card.Types.Concat(card.DeckTypes).Concat(card.Clans)), 16, Palette.PaperDim));
            if (card.Stats.Count > 0)
            {
                detail.AddChild(new HSeparator());
                detail.AddChild(Palette.Wrapped(string.Join("    ", card.Stats.Select(stat => $"{StatName(stat.Key)} {string.Join(" / ", stat.Value)}")), 17));
            }
            if (card.Keywords.Length > 0) detail.AddChild(Palette.Wrapped(string.Join(" · ", card.Keywords), 15, Palette.Gold));
            detail.AddChild(new HSeparator());
            detail.AddChild(Palette.Text("CURRENT ORACLE TEXT", 13, Palette.Muted));
            var displayedText = DisplayOracleText(card.CurrentOracleText);
            detail.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(displayedText) ? "No rules text recorded in the saved catalogue." : displayedText, 17));
            if (displayedText != card.CurrentOracleText)
                detail.AddChild(Palette.Wrapped("Gold and bow symbols are shown as words in brackets.", 14, Palette.Muted));
            detail.AddChild(Palette.Wrapped("Reference only. Review the Emperor printing before treating this wording as a historical rule. Source-linked scans are listed separately in Uploaded artwork; their printing era remains unverified.", 14, Palette.Muted));
            foreach (var reference in card.LegacyReferences)
            {
                var source = new VBoxContainer { Visible = false };
                source.AddThemeConstantOverride("separation", 12);
                var toggle = Palette.Button("Show 2017 source reference", () => source.Visible = !source.Visible);
                toggle.TooltipText = "Sun and Moon database wording and identity evidence";
                source.VisibilityChanged += () => toggle.Text = source.Visible
                    ? "Hide 2017 source reference" : "Show 2017 source reference";
                detail.AddChild(toggle);
                detail.AddChild(source);
                source.AddChild(Palette.Text("SUN AND MOON · UPDATED 2017", 13, Palette.Muted));
                source.AddChild(Palette.Wrapped($"Database {reference.DatabaseVersion} · source card ID {reference.SourceCardId}\nSource record {reference.LegacyCardId}", 14, Palette.PaperDim));
                source.AddChild(Palette.Wrapped(reference.TextComparison == "same_plain_text"
                    ? "Plain text matches current Oracle. Historical printing verification remains open."
                    : "Wording differs from current Oracle and needs review. This comparison does not identify which wording is historically correct.", 14, Palette.Gold));
                if (reference.MatchEvidence.Length > 0)
                    source.AddChild(Palette.Wrapped("Identity evidence: " + string.Join(" · ", reference.MatchEvidence), 14, Palette.Muted));
                if (reference.DeclaredLegalities.Length > 0)
                    source.AddChild(Palette.Wrapped("Source legality tags (unverified): " + string.Join(" · ", reference.DeclaredLegalities), 14, Palette.Muted));
                source.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(reference.LegacyText)
                    ? "No rules text recorded in this source record." : DisplayOracleText(reference.LegacyText), 17));
            }
            if (card.SourceUrl.StartsWith("https://oracleofthevoid.com/", StringComparison.Ordinal))
                detail.AddChild(Palette.Button("View source in browser ↗", () => OS.ShellOpen(card.SourceUrl)));
        }
    }

    private static string DisplayOracleText(string text)
    {
        // Display substitutions only: the packaged source text remains unchanged.
        var words = text.Replace(":bow:", "[Bow]", StringComparison.Ordinal);
        return Regex.Replace(words, @":g(\d+|star):", match =>
            $"[{(match.Groups[1].Value == "star" ? "*" : match.Groups[1].Value)} Gold]");
    }

    private static string PartitionName(string name) => name switch
    {
        "Pre-Game" => "Stronghold / pregame", "Starting holdings (selection unverified)" => "Starting holdings", _ => name
    };

    private static string StatName(string name) => name switch
    {
        "cost" => "Gold", "force" => "Force", "chi" => "Chi", "focus" => "Focus",
        "honor" => "Honor requirement", "ph" => "Personal honor", "production" => "Gold production",
        "startinghonor" => "Starting honor", "strength" => "Province strength", _ => name
    };

    private static Label Title(string text)
    {
        var label = Palette.Wrapped(text, 27);
        label.AddThemeFontOverride("font", Palette.Title);
        return label;
    }

    private static VBoxContainer Page(string name)
    {
        var page = new VBoxContainer { Name = name, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 16);
        return page;
    }

    private static VBoxContainer Column(int width, bool expand = false) => new()
    {
        CustomMinimumSize = new Vector2(width, 0),
        SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };

    private static ItemList List()
    {
        var list = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AutoHeight = false, AutoWidth = false, ClipContents = true,
            AllowReselect = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        list.AddThemeConstantOverride("v_separation", 12);
        list.AddThemeFontSizeOverride("font_size", 16);
        return list;
    }

    private static (ScrollContainer Scroll, VBoxContainer Content) DetailColumn()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var content = Column(0, true);
        content.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(content);
        return (scroll, content);
    }

    private static Theme LibraryTheme()
    {
        var theme = Palette.MakeTheme();
        theme.SetStylebox("panel", "AcceptDialog", Palette.Box(Palette.Ink, Palette.Gold, 20));
        theme.SetStylebox("panel", "TabContainer", Palette.Box(Palette.Ink, null, 18));
        theme.SetStylebox("tab_selected", "TabContainer", Palette.Box(new Color("364b3b"), Palette.Gold, 14));
        theme.SetStylebox("tab_unselected", "TabContainer", Palette.Box(new Color("22362c"), null, 14));
        theme.SetColor("font_selected_color", "TabContainer", Palette.Paper);
        theme.SetColor("font_unselected_color", "TabContainer", Palette.PaperDim);
        theme.SetStylebox("panel", "ItemList", Palette.Box(new Color("17281f"), new Color("405344"), 10));
        theme.SetStylebox("selected", "ItemList", Palette.Box(new Color("3b503f"), Palette.Gold, 6));
        theme.SetStylebox("selected_focus", "ItemList", Palette.Box(new Color("3b503f"), Palette.Gold, 6));
        theme.SetColor("font_color", "ItemList", Palette.PaperDim);
        theme.SetColor("font_selected_color", "ItemList", Palette.Paper);
        return theme;
    }

    private sealed class Bundle
    {
        public int SchemaVersion { get; set; }
        public CatalogueSource CatalogueSource { get; set; } = new();
        public Card[] Cards { get; set; } = [];
        public Deck[] Decks { get; set; } = [];
        public Artwork[] Artwork { get; set; } = [];
    }

    private sealed class CatalogueSource
    {
        public string RetrievedOn { get; set; } = "date unavailable";
    }

    private sealed class Card
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string[] Types { get; set; } = [];
        public string[] DeckTypes { get; set; } = [];
        public string[] Clans { get; set; } = [];
        public string[] Keywords { get; set; } = [];
        public Dictionary<string, string[]> Stats { get; set; } = [];
        public string CurrentOracleText { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public LegacyReference[] LegacyReferences { get; set; } = [];
    }

    private sealed class LegacyReference
    {
        public string LegacyCardId { get; set; } = "";
        public string SourceCardId { get; set; } = "";
        public string DatabaseVersion { get; set; } = "";
        public string LegacyText { get; set; } = "";
        public string TextComparison { get; set; } = "";
        public string[] DeclaredLegalities { get; set; } = [];
        public string[] MatchEvidence { get; set; } = [];
    }

    private sealed class Deck
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string DeclaredEra { get; set; } = "";
        public int CardCount { get; set; }
        public int EntryCount { get; set; }
        public Dictionary<string, int> PartitionCounts { get; set; } = [];
        public Dictionary<string, int> ResolutionCounts { get; set; } = [];
        public Entry[] Entries { get; set; } = [];
    }

    private sealed class Entry
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public string? CardId { get; set; }
        public string ResolutionStatus { get; set; } = "";
        public string ResolutionReason { get; set; } = "";
        public string[] Types { get; set; } = [];
        public string[] DeckTypes { get; set; } = [];
    }

    private sealed class Artwork
    {
        public string Filename { get; set; } = "";
        public string CardName { get; set; } = "";
        public string CardType { get; set; } = "";
        public string LegacyCardId { get; set; } = "";
        public string SourceCardId { get; set; } = "";
        public string ImageEdition { get; set; } = "";
        public string SourceImagePath { get; set; } = "";
        public string[] DeclaredLegalities { get; set; } = [];
        public string LegacyText { get; set; } = "";
        public string DatabaseVersion { get; set; } = "";
        public string MappingStatus { get; set; } = "unmapped";
        public string MappingReason { get; set; } = "";
        public string ResourcePath { get; set; } = "";
        public string SourceFolder { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
