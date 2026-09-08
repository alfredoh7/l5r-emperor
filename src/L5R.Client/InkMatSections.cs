using Godot;

namespace L5R.Client;

/// <summary>Public layout facts only. Bounds and lettering are in the unprojected table plane.</summary>
public sealed record InkMatSection(string Id, string Label, Rect2 Bounds, bool Active = false);

/// <summary>
/// Persistent ink under the playable cards. Layout submits resting footprints; pointer
/// hover and camera movement do not participate in this state or restart its painting.
/// </summary>
public partial class InkMatSections : Control
{
    private const float ResizeSeconds = .76f, RevealSeconds = .84f, RemoveSeconds = .56f;
    private readonly Dictionary<string, Section> _sections = new(StringComparer.Ordinal);
    private string? _matchKey;
    private bool _reducedMotion;
    private Texture2D? _edge, _cloud;
    private Shader? _shader;
    private InkLabelOverlay? _labelOverlay;
    private readonly Color _letter = new("e8d3a8");

    /// <summary>Includes a removed section only while its old ink is dissolving.</summary>
    public int SectionCount => _sections.Count;
    public bool ReducedMotion => _reducedMotion;
    public bool IsAnimating => _sections.Values.Any(s => s.Removing || s.Progress < 1 || s.Reveal < 1 || s.GhostProgress < 1 || Math.Abs(s.Active - (s.Target.Active ? 1 : 0)) > .001f);
    public IReadOnlyList<InkMatSection> Targets => _sections.Values.Where(s => !s.Removing).Select(s => s.Target).ToArray();
    public Rect2? CurrentBounds(string id) => _sections.TryGetValue(id, out var section) ? section.Current : null;

    /// <summary>
    /// Optional crisp type above the projected canvas. Project receives unprojected
    /// world points and returns viewport-global points; the returned input-transparent
    /// control should be parented inside the clipped table viewport.
    /// </summary>
    public Control CreateLabelOverlay(Func<Vector2, Vector2> projectToGlobal)
    {
        if (_labelOverlay is not null && IsInstanceValid(_labelOverlay))
            throw new InvalidOperationException("The mat already has a label overlay.");
        _labelOverlay = new InkLabelOverlay(this, projectToGlobal);
        QueueRedraw();
        return _labelOverlay;
    }

    private sealed class Section(InkMatSection target, InkStroke stroke, InkStroke ghost)
    {
        public InkMatSection Target = target;
        public Rect2 From = target.Bounds, Current = target.Bounds, GhostBounds = target.Bounds;
        public float Progress = 1, Reveal = 1, GhostProgress = 1, RemoveProgress, Active = target.Active ? 1 : 0;
        public bool Removing;
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
                state = new Section(descriptor, stroke, ghost) { Reveal = initial || reducedMotion ? 1 : 0 };
                _sections.Add(descriptor.Id, state);
            }
            else
            {
                var changed = !SameRect(state.Target.Bounds, rect);
                // Reappearing during dissolution resumes from the currently visible
                // contour; unrelated sections never have their progress reset.
                if (state.Removing)
                {
                    state.Reveal = reducedMotion ? 1 : Math.Clamp(1 - state.RemoveProgress, 0, 1);
                    state.Removing = false; state.RemoveProgress = 0;
                }
                if (changed)
                {
                    state.GhostBounds = state.Current;
                    state.GhostProgress = reducedMotion ? 1 : 0;
                    state.From = state.Current;
                    state.Progress = reducedMotion ? 1 : 0;
                    if (reducedMotion) state.Current = rect;
                }
                else if (state.Progress >= 1) state.Current = rect;
                state.Target = descriptor;
            }
            if (reducedMotion)
            {
                state.Current = state.From = descriptor.Bounds;
                state.Progress = state.Reveal = state.GhostProgress = 1;
                state.Active = descriptor.Active ? 1 : 0;
            }
            Present(state);
        }
        foreach (var (id, state) in _sections.ToArray())
        {
            if (present.Contains(id)) continue;
            if (reducedMotion) { RemoveSection(id, state); continue; }
            if (!state.Removing) { state.Removing = true; state.RemoveProgress = 0; }
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
        foreach (var (id, state) in _sections.ToArray())
        {
            state.Progress = Math.Min(1, state.Progress + elapsed / ResizeSeconds);
            state.Reveal = Math.Min(1, state.Reveal + elapsed / RevealSeconds);
            state.GhostProgress = Math.Min(1, state.GhostProgress + elapsed / ResizeSeconds);
            state.Active = Mathf.MoveToward(state.Active, state.Target.Active ? 1 : 0, elapsed / .35f);
            var weight = Ease(state.Progress);
            state.Current = new Rect2(state.From.Position.Lerp(state.Target.Bounds.Position, weight), state.From.Size.Lerp(state.Target.Bounds.Size, weight));
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
        state.Stroke.Present(state.Current.Size, state.Active, state.Reveal, state.Progress,
            state.Removing ? state.RemoveProgress : 0, 1);
        state.Ghost.Visible = state.GhostProgress < 1 && !_reducedMotion;
        if (state.Ghost.Visible)
        {
            state.Ghost.Position = state.GhostBounds.Position;
            state.Ghost.Size = state.GhostBounds.Size;
            state.Ghost.Present(state.GhostBounds.Size, state.Active, 1, 1, state.GhostProgress,
                .24f * (1 - Ease(state.GhostProgress)) * (state.Removing ? 1 - state.RemoveProgress : 1));
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

    public override void _Draw()
    {
        if (_labelOverlay is not null && IsInstanceValid(_labelOverlay) && _labelOverlay.IsInsideTree()) return;
        foreach (var state in _sections.Values)
        {
            var bounds = state.Current;
            var opacity = Ease(Math.Clamp(state.Reveal * 2, 0, 1)) * (state.Removing ? 1 - Ease(state.RemoveProgress) : 1);
            if (opacity <= .001f || string.IsNullOrWhiteSpace(state.Target.Label)) continue;
            // Center the real heading within the first 64 logical units. This is
            // reserved by layout, so neither a title nor its motif touches a card.
            var width = Math.Max(1, bounds.Size.X - 42);
            var fontSize = 36;
            while (fontSize > 20 && Palette.Title.GetStringSize(state.Target.Label, fontSize: fontSize).X > width) fontSize--;
            var baseline = bounds.Position + new Vector2(21, 32 + (Palette.Title.GetAscent(fontSize) - Palette.Title.GetDescent(fontSize)) / 2);
            var tint = _letter.Lerp(new Color("f6dfaf"), state.Active);
            DrawStringOutline(Palette.Title, baseline, state.Target.Label, HorizontalAlignment.Center, width, fontSize, 3, new Color(.035f, .05f, .065f, .85f * opacity));
            DrawString(Palette.Title, baseline, state.Target.Label, HorizontalAlignment.Center, width, fontSize, tint with { A = opacity * .94f });
        }
    }

    private partial class InkLabelOverlay(InkMatSections owner, Func<Vector2, Vector2> project) : Control
    {
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            FocusMode = FocusModeEnum.None;
            ClipContents = false;
            owner.QueueRedraw();
        }

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            if (!IsInstanceValid(owner) || !owner.IsInsideTree() || !owner.IsVisibleInTree()) return;
            var transform = GetGlobalTransform();
            var inverse = transform.AffineInverse();
            var screenScale = Math.Max(.1f, transform.X.Length());
            foreach (var state in owner._sections.Values)
            {
                var bounds = state.Current;
                var opacity = Ease(Math.Clamp(state.Reveal * 2, 0, 1)) * (state.Removing ? 1 - Ease(state.RemoveProgress) : 1);
                if (opacity <= .001f || string.IsNullOrWhiteSpace(state.Target.Label)) continue;
                var centerWorld = bounds.Position + new Vector2(bounds.Size.X / 2, 32);
                var center = inverse * project(centerWorld);
                var left = inverse * project(new Vector2(bounds.Position.X + 12, centerWorld.Y));
                var right = inverse * project(new Vector2(bounds.End.X - 12, centerWorld.Y));
                var width = Math.Max(1, left.DistanceTo(right));
                var localSize = Math.Max(1, (int)Math.Round(18 / screenScale));
                var minimumSize = Math.Max(1, (int)Math.Round(16 / screenScale));
                var label = state.Target.Label;
                while (localSize > minimumSize && Palette.Title.GetStringSize(label, fontSize: localSize).X > width) localSize--;
                if (Palette.Title.GetStringSize(label, fontSize: localSize).X > width && label.StartsWith("Province ", StringComparison.Ordinal))
                    label = "P. " + label[9..];
                var labelWidth = Math.Min(width, Palette.Title.GetStringSize(label, fontSize: localSize).X);
                // The baseline is centered on the same world-space top gutter as
                // the ink. Camera changes only its anchor, never its readability.
                var baseline = center + new Vector2(-labelWidth / 2, (Palette.Title.GetAscent(localSize) - Palette.Title.GetDescent(localSize)) / 2);
                var color = owner._letter.Lerp(new Color("f6dfaf"), state.Active) with { A = opacity * .97f };
                DrawStringOutline(Palette.Title, baseline, label, HorizontalAlignment.Center, labelWidth, localSize,
                    Math.Max(1, (int)Math.Round(2 / screenScale)), new Color(.035f, .05f, .065f, opacity * .88f));
                DrawString(Palette.Title, baseline, label, HorizontalAlignment.Center, labelWidth, localSize, color);
            }
        }
    }

    /// <summary>Only painted strokes use the grain shader; lettering remains crisp.</summary>
    private partial class InkStroke : Control
    {
        public Texture2D Edge = null!, Cloud = null!;
        private float _active;
        public InkStroke()
        {
            MouseFilter = MouseFilterEnum.Ignore; FocusMode = FocusModeEnum.None;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public void Present(Vector2 size, float active, float reveal, float reform, float dissolve, float opacity)
        {
            _active = active;
            var material = (ShaderMaterial)Material;
            material.SetShaderParameter("section_size", size);
            material.SetShaderParameter("reveal", reveal);
            material.SetShaderParameter("reform", reform);
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
            var motifWidth = Math.Min(108, Size.X * .21f);
            var motifHeight = motifWidth * Cloud.GetHeight() / Math.Max(1, Cloud.GetWidth());
            var height = Math.Min(32, motifHeight);
            // Opposite corner curls echo the approved painted motif, with clear
            // central header space and a fixed scale as the frame grows.
            if (Size.X >= 350 && Size.Y >= 100)
            {
                DrawTextureRect(Cloud, new Rect2(7, 5, motifWidth, height), false, color with { A = .68f + _active * .10f });
                DrawTextureRect(Cloud, new Rect2(Size.X - 7 - motifWidth, Size.Y - 5 - height, -motifWidth, -height), false, color with { A = .68f + _active * .10f });
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
