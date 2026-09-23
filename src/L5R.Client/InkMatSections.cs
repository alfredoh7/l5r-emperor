using Godot;

namespace L5R.Client;

/// <summary>Public layout facts only. Bounds and icon identities are in the unprojected table plane.</summary>
public sealed record InkMatSection(string Id, string Label, Rect2 Bounds, bool Active = false, int? ProvinceNumber = null, bool ConnectionAbove = false, bool Connected = false);

/// <summary>
/// Persistent ink under the playable cards. Layout submits resting footprints; pointer
/// hover changes only emphasis; inspection and camera movement never restart its painting.
/// </summary>
public partial class InkMatSections : Control
{
    // Keep the organic ink sequence, with 35% less waiting/spreading time.
    // Actual card movement still finishes before a new contour starts.
    private const float InkTimingScale = .65f;
    private const float SettlePause = .16f * InkTimingScale,
        RevealSeconds = 1.35f * InkTimingScale, RemoveSeconds = .48f * InkTimingScale;
    public Func<bool> MotionSettled { get; set; } = () => true;
    private readonly Dictionary<string, Section> _sections = new(StringComparer.Ordinal);
    private string? _matchKey, _hoverId, _selectedId;
    private bool _reducedMotion;
    private Shader? _shader;
    private InkIconOverlay? _iconOverlay;
    public const float Header = 48, BattleFooter = 84;

    /// <summary>Includes a removed section only while its old ink is dissolving.</summary>
    public int SectionCount => _sections.Count;
    public bool ReducedMotion => _reducedMotion;
    public bool IsAnimating => _sections.Values.Any(s => s.Removing || s.Waiting || s.Reveal < 1 || s.GhostProgress < 1 || Math.Abs(s.Active - DesiredActive(s)) > .001f);
    public IReadOnlyList<InkMatSection> Targets => _sections.Values.Where(s => !s.Removing).Select(s => s.Target).ToArray();
    public float RevealProgress(string id) => _sections.TryGetValue(id, out var s) ? s.Reveal : 0;
    public bool WaitingForCards(string id) => _sections.TryGetValue(id, out var s) && s.Waiting;
    public Rect2? CurrentBounds(string id) => _sections.TryGetValue(id, out var section) ? section.Current : null;

    private float DesiredActive(Section s) => s.Target.Active || s.Target.Id == _hoverId || s.Target.Id == _selectedId ? 1 : 0;
    public float EmphasisProgress(string id) => _sections.TryGetValue(id, out var s) ? s.Active : 0;
    public void Emphasize(string? id, bool hover = false)
    {
        if ((hover ? _hoverId : _selectedId) == id) return;
        if (hover) _hoverId = id; else _selectedId = id;
        foreach (var state in _sections.Values)
        {
            if (_reducedMotion) state.Active = DesiredActive(state);
            Present(state);
        }
        SetProcess(IsAnimating);
    }

    /// <summary>
    /// Input-transparent hit regions for the emblems painted on the mat. Project receives unprojected
    /// world points and returns viewport-global points; the returned input-transparent
    /// control should be parented with the table layer, below the fixed HUD.
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
            _hoverId = _selectedId = null;
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
                state.Active = DesiredActive(state);
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
        _shader ??= GD.Load<Shader>("res://Art/ink-section.gdshader");
    }

    private InkStroke NewStroke()
    {
        var stroke = new InkStroke();
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
            state.Active = Mathf.MoveToward(state.Active, DesiredActive(state), elapsed / .35f);
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
        state.Stroke.ConnectionAbove = state.Target.ConnectionAbove; state.Stroke.Connected = state.Target.Connected;
        state.Stroke.Label = state.Target.Label; state.Stroke.ProvinceNumber = state.Target.ProvinceNumber;
        state.Stroke.Present(state.Current.Size, state.Active, state.Reveal,
            state.Removing ? state.RemoveProgress : 0, 1);
        state.Ghost.Visible = state.GhostProgress < 1 && !_reducedMotion;
        if (state.Ghost.Visible)
        {
            state.Ghost.Position = state.GhostBounds.Position;
            state.Ghost.Size = state.GhostBounds.Size;
            state.Ghost.ConnectionAbove = state.Target.ConnectionAbove; state.Ghost.Connected = state.Target.Connected;
            state.Ghost.Label = state.GhostLabel; state.Ghost.ProvinceNumber = state.Target.ProvinceNumber;
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

    public Control? IconAt(Vector2 globalPosition) => _iconOverlay?.Hit(globalPosition);
    public IReadOnlyList<Control> IconHotspots => _iconOverlay?.Hotspots ?? [];

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
        private Rect2 IconRect(Rect2 bounds, string label, bool province, bool connectionAbove)
        {
            var inverse = GetGlobalTransform().AffineInverse();
            var rect = province ? ProvinceThresholdArt.ThresholdBounds(bounds) : ProvinceThresholdArt.IconBounds(bounds, label, connectionAbove);
            var points = new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) }
                .Select(p => inverse * project(p)).ToArray();
            var minimum = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
            var maximum = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
            return new Rect2(minimum, maximum - minimum);
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
                    "Removed" => "Removed from the Game · Both players may inspect. Cannot be targeted or returned.",
                    "Provinces" => "Provinces · The seals below the cards identify each province",
                    _ => target.Label + " battlefield"
                });
                if (target.ProvinceNumber is not null)
                {
                    hit.SetMeta("province_marker", target.Id);
                    hit.SetMeta("flat_tooltip", target.Label + " · Surviving province");
                }
                var rect = IconRect(target.Bounds, target.Label, target.ProvinceNumber is not null, target.ConnectionAbove);
                hit.Position = rect.Position; hit.Size = rect.Size;
                hit.Visible = owner.IsVisibleInTree() && owner.RevealProgress(target.Id) > .85f;
            }
            var existing = owner.Targets.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var key in _hotspots.Keys.Where(key => !existing.Contains(key)).ToArray())
            { var old = _hotspots[key]; RemoveChild(old); old.QueueFree(); _hotspots.Remove(key); }
            QueueRedraw();
        }
    }

    /// <summary>Source artwork shares one table plane and one organic reveal shader.</summary>
    private partial class InkStroke : Control
    {
        public string Label = "Home";
        public int? ProvinceNumber;
        private float _active;
        public bool ConnectionAbove, Connected;
        public InkStroke()
        {
            MouseFilter = MouseFilterEnum.Ignore; FocusMode = FocusModeEnum.None;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public void Present(Vector2 size, float active, float reveal, float dissolve, float opacity)
        {
            _active = active;
            var material = (ShaderMaterial)Material;
            // Red is exclusively the committed-attack layer; hover remains warm gold.
            material.SetShaderParameter("battle_emphasis", 0);
            material.SetShaderParameter("section_size", size);
            material.SetShaderParameter("reveal", reveal);
            material.SetShaderParameter("dissolve", dissolve);
            material.SetShaderParameter("opacity", opacity);
            QueueRedraw();
        }

        public override void _Draw()
        {
            ProvinceThresholdArt.Draw(this, Size, Label, ProvinceNumber, _active, ConnectionAbove, Connected);
        }

    }
}
