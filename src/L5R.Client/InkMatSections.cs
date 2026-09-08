using Godot;

namespace L5R.Client;

/// <summary>Public layout facts only. Bounds and icon identities are in the unprojected table plane.</summary>
public sealed record InkMatSection(string Id, string Label, Rect2 Bounds, bool Active = false);

/// <summary>
/// Persistent ink under the playable cards. Layout submits resting footprints; pointer
/// hover and camera movement do not participate in this state or restart its painting.
/// </summary>
public partial class InkMatSections : Control
{
    private const float SettlePause = .16f, RevealSeconds = 1.35f, RemoveSeconds = .48f;
    public Func<bool> MotionSettled { get; set; } = () => true;
    private readonly Dictionary<string, Section> _sections = new(StringComparer.Ordinal);
    private string? _matchKey;
    private bool _reducedMotion;
    private Texture2D? _edge, _cloud;
    private Shader? _shader;
    private InkIconOverlay? _iconOverlay;
    public const float Header = 96, BattleFooter = 84;

    /// <summary>Includes a removed section only while its old ink is dissolving.</summary>
    public int SectionCount => _sections.Count;
    public bool ReducedMotion => _reducedMotion;
    public bool IsAnimating => _sections.Values.Any(s => s.Removing || s.Waiting || s.Reveal < 1 || s.GhostProgress < 1 || Math.Abs(s.Active - (s.Target.Active ? 1 : 0)) > .001f);
    public IReadOnlyList<InkMatSection> Targets => _sections.Values.Where(s => !s.Removing).Select(s => s.Target).ToArray();
    public float RevealProgress(string id) => _sections.TryGetValue(id, out var s) ? s.Reveal : 0;
    public bool WaitingForCards(string id) => _sections.TryGetValue(id, out var s) && s.Waiting;
    public Rect2? CurrentBounds(string id) => _sections.TryGetValue(id, out var section) ? section.Current : null;

    /// <summary>
    /// Optional crisp emblems above the projected canvas. Project receives unprojected
    /// world points and returns viewport-global points; the returned input-transparent
    /// control should be parented inside the clipped table viewport.
    /// </summary>
    public Control CreateIconOverlay(Func<Vector2, Vector2> projectToGlobal)
    {
        if (_iconOverlay is not null && IsInstanceValid(_iconOverlay))
            throw new InvalidOperationException("The mat already has an icon overlay.");
        _iconOverlay = new InkIconOverlay(this, projectToGlobal);
        QueueRedraw();
        return _iconOverlay;
    }

    private sealed class Section(InkMatSection target, InkStroke stroke, InkStroke ghost)
    {
        public InkMatSection Target = target;
        public Rect2 Current = target.Bounds, GhostBounds = target.Bounds;
        public float Reveal = 1, GhostReveal = 1, GhostProgress = 1, RemoveProgress, QuietTime, Active = target.Active ? 1 : 0;
        public bool Removing, Waiting;
        public string GhostLabel = target.Label;
        public InkStroke Stroke = stroke, Ghost = ghost;
    }

    public InkMatSections()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ClipContents = false;
    }

    public override void _Ready() => SetProcess(IsAnimating);

    public void Sync(IReadOnlyList<InkMatSection> sections, bool reducedMotion, string matchKey)
    {
        var initial = _matchKey != matchKey;
        if (initial)
        {
            ClearSections();
            _matchKey = matchKey;
        }
        _reducedMotion = reducedMotion;
        LoadArt();
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in sections)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Id) || !present.Add(descriptor.Id))
                throw new ArgumentException("Mat sections require distinct nonempty identifiers.", nameof(sections));
            var rect = descriptor.Bounds;
            if (!float.IsFinite(rect.Position.X) || !float.IsFinite(rect.Position.Y) || !float.IsFinite(rect.Size.X) || !float.IsFinite(rect.Size.Y) || rect.Size.X <= 0 || rect.Size.Y <= 0)
                throw new ArgumentException("Mat section bounds must be finite and have positive area.", nameof(sections));
            if (!_sections.TryGetValue(descriptor.Id, out var state))
            {
                var ghost = NewStroke(); var stroke = NewStroke();
                state = new Section(descriptor, stroke, ghost) { Reveal = initial || reducedMotion ? 1 : 0, Waiting = !initial && !reducedMotion };
                _sections.Add(descriptor.Id, state);
            }
            else
            {
                var changed = !SameRect(state.Target.Bounds, rect);
                if (changed || state.Removing)
                {
                    // Preserve the old contour at its settled location while it
                    // evaporates. New ink has no moving/interpolated rectangle.
                    if (state.Reveal > .001f)
                    {
                        state.GhostBounds = state.Current;
                        state.GhostLabel = state.Target.Label;
                        state.GhostReveal = state.Reveal;
                        state.GhostProgress = reducedMotion ? 1 : 0;
                    }
                    state.Current = rect;
                    state.Reveal = reducedMotion ? 1 : 0;
                    state.Waiting = !reducedMotion;
                    state.QuietTime = 0;
                    state.Removing = false; state.RemoveProgress = 0;
                }
                state.Target = descriptor;
            }
            if (reducedMotion)
            {
                state.Current = descriptor.Bounds;
                state.Reveal = state.GhostProgress = 1;
                state.Waiting = false;
                state.Active = descriptor.Active ? 1 : 0;
            }
            Present(state);
        }
        foreach (var (id, state) in _sections.ToArray())
        {
            if (present.Contains(id)) continue;
            if (reducedMotion) { RemoveSection(id, state); continue; }
            if (!state.Removing) { state.Removing = true; state.Waiting = false; state.RemoveProgress = 0; }
            Present(state);
        }
        QueueRedraw();
        SetProcess(IsAnimating);
    }

    private void LoadArt()
    {
        if (_edge is not null) return;
        _edge = TrimTransparent("res://Art/mat-ink-edge.png");
        _cloud = TrimTransparent("res://Art/mat-ink-cloud.png");
        _shader = GD.Load<Shader>("res://Art/ink-section.gdshader");
    }

    private static Texture2D TrimTransparent(string path)
    {
        var source = GD.Load<Texture2D>(path);
        using var bitmap = source.GetImage();
        if (bitmap.IsCompressed()) bitmap.Decompress();
        bitmap.Convert(Image.Format.Rgba8);
        var pixels = bitmap.GetData();
        var width = bitmap.GetWidth(); var height = bitmap.GetHeight();
        var left = width; var top = height; var right = -1; var bottom = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                // Generated cutouts contain nearly transparent stray pixels far
                // from their pigment. Ignore those when finding the atlas bounds,
                // without modifying or keying any pixels in the original artwork.
                if (pixels[(y * width + x) * 4 + 3] <= 8) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
            }
        return right >= left && bottom >= top
            ? new AtlasTexture { Atlas = source, Region = new Rect2(left, top, right - left + 1, bottom - top + 1) }
            : source;
    }

    private InkStroke NewStroke()
    {
        var stroke = new InkStroke { Edge = _edge!, Cloud = _cloud! };
        stroke.Material = new ShaderMaterial { Shader = _shader };
        AddChild(stroke);
        return stroke;
    }

    public override void _Process(double delta)
    {
        var elapsed = (float)Math.Min(delta, .1);
        var cardsSettled = !_sections.Values.Any(s => s.Waiting) || MotionSettled();
        foreach (var (id, state) in _sections.ToArray())
        {
            state.GhostProgress = Math.Min(1, state.GhostProgress + elapsed / RemoveSeconds);
            state.Active = Mathf.MoveToward(state.Active, state.Target.Active ? 1 : 0, elapsed / .35f);
            if (state.Waiting)
            {
                state.QuietTime = cardsSettled ? state.QuietTime + elapsed : 0;
                if (state.QuietTime >= SettlePause && state.GhostProgress >= 1) state.Waiting = false;
            }
            else if (!state.Removing) state.Reveal = Math.Min(1, state.Reveal + elapsed / RevealSeconds);
            if (state.Removing)
            {
                state.RemoveProgress = Math.Min(1, state.RemoveProgress + elapsed / RemoveSeconds);
                if (state.RemoveProgress >= 1) { RemoveSection(id, state); continue; }
            }
            Present(state);
        }
        QueueRedraw();
        SetProcess(IsAnimating);
    }

    private void Present(Section state)
    {
        state.Stroke.Position = state.Current.Position;
        state.Stroke.Size = state.Current.Size;
        state.Stroke.Present(state.Current.Size, state.Active, state.Reveal,
            state.Removing ? state.RemoveProgress : 0, 1);
        state.Ghost.Visible = state.GhostProgress < 1 && !_reducedMotion;
        if (state.Ghost.Visible)
        {
            state.Ghost.Position = state.GhostBounds.Position;
            state.Ghost.Size = state.GhostBounds.Size;
            state.Ghost.Present(state.GhostBounds.Size, state.Active, state.GhostReveal, state.GhostProgress,
                1 - Ease(state.GhostProgress));
        }
    }

    private void ClearSections()
    {
        foreach (var (id, section) in _sections.ToArray()) RemoveSection(id, section);
    }

    private void RemoveSection(string id, Section state)
    {
        foreach (var stroke in new[] { state.Stroke, state.Ghost })
        {
            RemoveChild(stroke); stroke.QueueFree();
        }
        _sections.Remove(id);
    }

    private static bool SameRect(Rect2 a, Rect2 b) => a.Position.DistanceSquaredTo(b.Position) < .01f && a.Size.DistanceSquaredTo(b.Size) < .01f;
    private static float Ease(float value) => value * value * value * (value * (value * 6 - 15) + 10);

    private IEnumerable<(Rect2 Bounds, string Label, float Opacity, float Active)> Emblems()
    {
        foreach (var state in _sections.Values)
        {
            var fade = state.Removing ? 1 - Ease(state.RemoveProgress) : 1;
            if (state.GhostProgress < 1)
                yield return (state.GhostBounds, state.GhostLabel,
                    Ease(Math.Clamp((state.GhostReveal-.2f)/.65f,0,1)) * (1-Ease(state.GhostProgress)) * fade, state.Active);
            yield return (state.Current, state.Target.Label,
                Ease(Math.Clamp((state.Reveal-.2f)/.65f,0,1)) * fade, state.Active);
        }
    }

    public Control? IconAt(Vector2 globalPosition) => _iconOverlay?.Hit(globalPosition);
    public IReadOnlyList<Control> IconHotspots => _iconOverlay?.Hotspots ?? [];

    public override void _Draw()
    {
        if (_iconOverlay is not null && IsInstanceValid(_iconOverlay) && _iconOverlay.IsInsideTree()) return;
        foreach (var state in Emblems())
        {
            if (state.Opacity <= .001f) continue;
            var texture = MatEmblemArt.Symbol(state.Label);
            var height = 64f;
            var size = new Vector2(height * texture.GetWidth() / texture.GetHeight(), height);
            DrawTextureRect(texture, new Rect2(state.Bounds.Position + new Vector2(state.Bounds.Size.X / 2, Header / 2) - size / 2, size), false,
                new Color(1, 1, 1, state.Opacity * .9f));
        }
    }

    private partial class InkIconOverlay(InkMatSections owner, Func<Vector2, Vector2> project) : Control
    {
        private readonly Dictionary<string, Control> _hotspots = new(StringComparer.Ordinal);
        public IReadOnlyList<Control> Hotspots => _hotspots.Values.ToArray();
        public Control? Hit(Vector2 global) => IsVisibleInTree()
            ? _hotspots.Values.FirstOrDefault(c => c.Visible && c.GetGlobalRect().HasPoint(global)) : null;
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            FocusMode = FocusModeEnum.None;
            ClipContents = false;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            owner.QueueRedraw();
        }
        private Rect2 IconRect(Rect2 bounds, string label)
        {
            var inverse = GetGlobalTransform().AffineInverse();
            var centerWorld = bounds.Position + new Vector2(bounds.Size.X / 2, Header / 2);
            var center = inverse * project(centerWorld);
            var availableHeight = (inverse * project(centerWorld + new Vector2(0, Header / 2 - 6)))
                .DistanceTo(inverse * project(centerWorld - new Vector2(0, Header / 2 - 6)));
            var screenScale = Math.Max(.1f, GetGlobalTransform().X.Length());
            var height = Math.Min(30 / screenScale, availableHeight * .84f);
            var texture = MatEmblemArt.Symbol(label);
            var size = new Vector2(height * texture.GetWidth() / texture.GetHeight(), height);
            return new Rect2(center - size / 2, size);
        }
        public override void _Process(double delta)
        {
            if (!IsInstanceValid(owner) || !owner.IsInsideTree()) return;
            foreach (var target in owner.Targets)
            {
                if (!_hotspots.TryGetValue(target.Id, out var hit))
                {
                    hit = new Control { MouseFilter = MouseFilterEnum.Ignore, FocusMode = FocusModeEnum.None };
                    hit.SetMeta("mat_icon", target.Id);
                    _hotspots.Add(target.Id, hit); AddChild(hit);
                }
                // Explicit hit testing gives the projected mat readable tooltips
                // without any overlay intercepting a card, click or camera drag.
                hit.SetMeta("flat_tooltip", target.Label switch
                {
                    "Holdings" => "Holdings · Stronghold and in-play Holdings",
                    "Home" => "Home · Cards at home and their attachments",
                    "Provinces" => "Provinces · The seals below the cards identify each province",
                    _ => target.Label + " battlefield"
                });
                var rect = IconRect(target.Bounds, target.Label);
                hit.Position = rect.Position; hit.Size = rect.Size;
                hit.Visible = owner.IsVisibleInTree() && owner.RevealProgress(target.Id) > .85f;
            }
            var existing = owner.Targets.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var key in _hotspots.Keys.Where(key => !existing.Contains(key)).ToArray())
            { var old = _hotspots[key]; RemoveChild(old); old.QueueFree(); _hotspots.Remove(key); }
            QueueRedraw();
        }
        public override void _Draw()
        {
            if (!IsInstanceValid(owner) || !owner.IsInsideTree() || !owner.IsVisibleInTree()) return;
            var inverse = GetGlobalTransform().AffineInverse();
            var screenScale = Math.Max(.1f, GetGlobalTransform().X.Length());
            foreach (var state in owner.Emblems())
            {
                if (state.Opacity <= .001f) continue;
                var tint = new Color(1, 1, 1, state.Opacity * (.88f + .12f * state.Active));
                DrawTextureRect(MatEmblemArt.Symbol(state.Label), IconRect(state.Bounds, state.Label), false, tint);
                if (!state.Label.StartsWith("Province ", StringComparison.Ordinal)) continue;
                // The battlefield keeps its destination identity, also beneath
                // its units. No permanently visible words are needed on the mat.
                var centerWorld = state.Bounds.Position + new Vector2(state.Bounds.Size.X / 2, state.Bounds.Size.Y - BattleFooter / 2);
                var center = inverse * project(centerWorld);
                var available = (inverse * project(centerWorld + new Vector2(0, BattleFooter / 2 - 4)))
                    .DistanceTo(inverse * project(centerWorld - new Vector2(0, BattleFooter / 2 - 4)));
                var height = Math.Min(30 / screenScale, available * .86f);
                var size = new Vector2(height * MatEmblemArt.Seal.GetWidth() / MatEmblemArt.Seal.GetHeight(), height);
                MatEmblemArt.DrawSeal(this, new Rect2(center - size / 2, size), state.Label[9..], state.Opacity);
            }
        }
    }

    /// <summary>Only painted strokes use the grain shader; emblems remain crisp.</summary>
    private partial class InkStroke : Control
    {
        public Texture2D Edge = null!, Cloud = null!;
        private float _active;
        public InkStroke()
        {
            MouseFilter = MouseFilterEnum.Ignore; FocusMode = FocusModeEnum.None;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public void Present(Vector2 size, float active, float reveal, float dissolve, float opacity)
        {
            _active = active;
            var material = (ShaderMaterial)Material;
            material.SetShaderParameter("section_size", size);
            material.SetShaderParameter("reveal", reveal);
            material.SetShaderParameter("dissolve", dissolve);
            material.SetShaderParameter("opacity", opacity);
            QueueRedraw();
        }

        public override void _Draw()
        {
            // Texture grain repeats at a fixed world scale. A wider field gets
            // more ink, never stretched clouds or oversized corner ornaments.
            var color = new Color("ecdbb6").Lerp(new Color("ffebbf"), _active);
            var strength = .72f + _active * .14f;
            DrawBorder(22, color with { A = .13f + _active * .05f });
            DrawBorder(9, color with { A = strength });
            var motifWidth = Math.Min(54, Size.X * .12f);
            var motifHeight = motifWidth * Cloud.GetHeight() / Math.Max(1, Cloud.GetWidth());
            var height = Math.Min(16, motifHeight);
            // Opposite corner curls echo the approved painted motif, with clear
            // central header space and a fixed scale as the frame grows.
            if (Size.X >= 350 && Size.Y >= 100)
            {
                DrawTextureRect(Cloud, new Rect2(7, 5, motifWidth, height), false, color with { A = .55f + _active * .10f });
                DrawTextureRect(Cloud, new Rect2(Size.X - 7 - motifWidth, Size.Y - 5 - height, -motifWidth, -height), false, color with { A = .55f + _active * .10f });
            }
        }

        private void DrawBorder(float thickness, Color color)
        {
            // All four sides use one physical grain scale. The diffuse pass is
            // wider but retains that grain scale, like ink soaking into the cloth.
            var pitch = Math.Clamp(Edge.GetWidth() / (float)Math.Max(1, Edge.GetHeight()) * 5.4f, 65, 180);
            DrawEdge(Vector2.Zero, 0, Size.X, thickness, pitch, color);
            DrawEdge(new Vector2(Size.X, 0), Mathf.Pi / 2, Size.Y, thickness, pitch, color);
            DrawEdge(Size, Mathf.Pi, Size.X, thickness, pitch, color);
            DrawEdge(new Vector2(0, Size.Y), -Mathf.Pi / 2, Size.Y, thickness, pitch, color);
        }

        private void DrawEdge(Vector2 origin, float angle, float length, float thickness, float pitch, Color color)
        {
            var direction = Vector2.FromAngle(angle);
            var normal = new Vector2(-direction.Y, direction.X) * thickness / 2;
            var source = Edge is AtlasTexture atlas ? atlas.Atlas : Edge;
            var region = Edge is AtlasTexture cropped ? cropped.Region : new Rect2(Vector2.Zero, Edge.GetSize());
            for (var offset = 0f; offset < length; offset += pitch)
            {
                var part = Math.Min(pitch, length - offset);
                var start = origin + direction * offset;
                var end = start + direction * (part + .15f);
                Vector2[] points = [start - normal, end - normal, end + normal, start + normal];
                var uvEnd = region.Position + new Vector2(region.Size.X * part / pitch, region.Size.Y);
                Vector2[] uv = [region.Position / source.GetSize(), new Vector2(uvEnd.X, region.Position.Y) / source.GetSize(),
                    uvEnd / source.GetSize(), new Vector2(region.Position.X, uvEnd.Y) / source.GetSize()];
                // Explicit vertices keep the shader's local_point in section
                // coordinates, including the vertical and reversed edges.
                DrawColoredPolygon(points, color, uv, source);
            }
        }
    }
}
