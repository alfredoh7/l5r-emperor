using System.Diagnostics;
using Godot;
using L5R.AI;
using L5R.Core;

namespace L5R.Client;

/// <summary>Issue #875: <c>--perf=menu|table|play</c> runs a scenario for <c>--perf-seconds</c> (default 12), prints one PERF line a second and quits.</summary>
public partial class Main
{
    private string? _perfScenario; private double _perfSeconds = 12, _perfClock, _perfNext = 1, _perfStep; private long _perfCommands;
    private TimeSpan _perfCpu; private long _perfWall; private long _perfAlloc; private int[] _perfGc = [0, 0, 0]; private readonly List<double> _perfFrames = [];

    private void StartPerf(string scenario)
    {
        _perfScenario = scenario;
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--perf-seconds=")) _perfSeconds = double.Parse(arg[15..], System.Globalization.CultureInfo.InvariantCulture);
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--perf-toggle=")) _perfToggle = arg[14..];
        _perfDraws = OS.GetCmdlineUserArgs().Contains("--perf-draws");
        foreach (var arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--perf-shot=")) _perfShot = arg[12..];
        if (!OS.GetCmdlineUserArgs().Contains("--perf-vsync")) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        if (scenario != "menu") { _session.StartPractice(false, true, 1); EnterTable(); }
        _perfCpu = Process.GetCurrentProcess().TotalProcessorTime; _perfWall = Stopwatch.GetTimestamp(); _perfAlloc = GC.GetTotalAllocatedBytes();
        for (var g = 0; g < 3; g++) _perfGc[g] = GC.CollectionCount(g);
        GD.Print($"PERF_START scenario={scenario} renderer={RenderingServer.GetCurrentRenderingMethod()} adapter={RenderingServer.GetVideoAdapterName()}");
    }

    private bool _perfDraws; private string? _perfShot; private bool _perfShotDone;
    // --perf-shot=path: a deterministic picture of the 3D hall alone (static light, 2D hidden) for before/after comparison.
    private void ProcessPerfShot()
    {
        // The auto-fitted camera settles to slightly different values from run to run; pin it so shots compare pixel for pixel.
        if (_perfClock > 1 && _perfClock < 10) { _cameraGlide = false; _cameraFitted = false; _cameraCenter = new Vector2(974.4286f, 524); _cameraZoom = .4693618f; ApplyTableCamera(); }
        if (_perfClock > 1 && _root.Visible) { _table.ReducedMotion = true; if (!OS.GetCmdlineUserArgs().Contains("--perf-full")) _root.Hide(); }
        if (_perfClock < 10 || _perfShotDone) return;
        _perfShotDone = true;
        GetViewport().GetTexture().GetImage().SavePng(_perfShot);
        GD.Print($"PERF_SHOT {_perfShot}");
    }

    // --perf-draws: hides each canvas subtree in turn (frame by frame) and reports how many draw calls it was responsible for.
    private List<(string Name, CanvasItem Node)>? _drawCandidates; private int _drawIndex = -1, _drawWait; private double _drawBase;
    private readonly List<string> _drawReport = [];
    private void ProcessPerfDraws()
    {
        _table.ReducedMotion = true; // a still hall, so the 30 Hz hall redraw does not blur the counts
        if (_perfClock < 4) return;
        var draws = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        if (_drawCandidates is null)
        {
            _drawCandidates = [];
            void Collect(Node n, string path, int depth)
            {
                foreach (var c in n.GetChildren())
                {
                    if (c is not CanvasItem ci || !ci.Visible) continue;
                    var name = $"{path}/{c.Name}:{c.GetType().Name}"; _drawCandidates.Add((name, ci));
                    if (depth < 1) Collect(c, name, depth + 1);
                }
            }
            Collect(_world, "world", 0); Collect(_root, "root", 0);
            _drawBase = draws; _drawIndex = -1; _drawWait = 3;
            GD.Print($"PERF_DRAWS_BASE {draws} candidates={_drawCandidates.Count}");
            return;
        }
        if (_drawWait-- > 0) return;
        if (_drawIndex >= 0) { var (n, node) = _drawCandidates[_drawIndex]; GD.Print($"PERF_DRAWS {_drawBase - draws,5:0} {n}"); node.Visible = true; }
        _drawIndex++;
        if (_drawIndex >= _drawCandidates.Count) { GetTree().Quit(); return; }
        _drawCandidates[_drawIndex].Node.Visible = false; _drawWait = 3;
    }

    private string _perfToggle = ""; private bool _perfToggled;
    private void ApplyPerfToggle()
    {
        _perfToggled = true;
        void Walk(Node n)
        {
            switch (_perfToggle)
            {
                case "shadows" when n is Light3D l: l.ShadowEnabled = false; break;
                case "piles" when n is TablePile pile: pile.Visible = false; break;
                case "lights" when n is OmniLight3D o: o.Visible = false; break;
                case "hall" when n is FortressHall h: h.Visible = false; break;
                case "oldcast" when n is MeshInstance3D m && m.Name == "StaticHallPartition": m.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; break;
                case "noshadowcast" when n is GeometryInstance3D g: g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; break;
            }
            foreach (var c in n.GetChildren()) Walk(c);
        }
        Walk(GetTree().Root);
        GD.Print($"PERF_TOGGLE {_perfToggle}");
    }

    private void ProcessPerf(double delta)
    {
        if (!_perfToggled && _perfToggle != "" && _perfClock > .5) ApplyPerfToggle();
        _perfClock += delta; _perfFrames.Add(delta * 1000);
        if (_perfShot is not null) ProcessPerfShot();
        if (_perfDraws) { ProcessPerfDraws(); return; }
        if (_perfScenario == "play" && _session.Snapshot is not null && View.WinnerId is null && (_perfStep += delta) > .15)
        {
            _perfStep = 0;
            try
            {
                _session.FollowPracticePriority();
                var command = TrainingOpponent.Choose(View, Actions).Command;
                if (command is not null) { _session.Submit(command).GetAwaiter().GetResult(); _perfCommands++; }
            }
            catch (Exception e) { GD.Print($"PERF_NOTE play stopped: {e.Message}"); _perfScenario = "table"; }
        }
        if (_perfClock < _perfNext) return;
        _perfNext += 1;
        var proc = Process.GetCurrentProcess(); var now = Stopwatch.GetTimestamp();
        var wall = Stopwatch.GetElapsedTime(_perfWall).TotalSeconds; var cpu = (proc.TotalProcessorTime - _perfCpu).TotalSeconds;
        var vp = GetViewport().GetViewportRid();
        _perfFrames.Sort(); var p95 = _perfFrames[(int)(_perfFrames.Count * .95)]; var avg = _perfFrames.Average();
        GD.Print($"PERF t={_perfClock:0} fps={Engine.GetFramesPerSecond()} frame_ms_avg={avg:0.00} frame_ms_p95={p95:0.00} process_ms={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.00} " +
                 $"render_cpu_ms={RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp):0.00} render_gpu_ms={RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp):0.00} cpu_pct={cpu / wall * 100:0} rss_mb={proc.WorkingSet64 / 1048576.0:0} managed_mb={GC.GetTotalMemory(false) / 1048576.0:0.0} " +
                 $"alloc_kb_s={(GC.GetTotalAllocatedBytes() - _perfAlloc) / 1024.0 / wall:0} gc0={GC.CollectionCount(0) - _perfGc[0]} gc1={GC.CollectionCount(1) - _perfGc[1]} gc2={GC.CollectionCount(2) - _perfGc[2]} " +
                 $"nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} objects={Performance.GetMonitor(Performance.Monitor.ObjectCount):0} resources={Performance.GetMonitor(Performance.Monitor.ObjectResourceCount):0} " +
                 $"draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} tex_mb={Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / 1048576:0} vram_mb={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0} " +
                 $"static_mb={Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576:0} commands={_perfCommands}");
        _perfCpu = proc.TotalProcessorTime; _perfWall = now; _perfAlloc = GC.GetTotalAllocatedBytes(); _perfFrames.Clear();
        for (var g = 0; g < 3; g++) _perfGc[g] = GC.CollectionCount(g);
        if (_perfClock >= _perfSeconds) { DumpPerfNodes(); DumpPerfTextures(); GetTree().Quit(); }
    }

    /// <summary>Counts live nodes by type, and how many of each type run _Process, to show what keeps working on an idle table.</summary>
    private void DumpPerfNodes()
    {
        var all = new Dictionary<string, (int Nodes, int Processing, int Visible)>();
        void Walk(Node n)
        {
            var key = n.GetType().Name; all.TryGetValue(key, out var v);
            all[key] = (v.Nodes + 1, v.Processing + (n.IsProcessing() ? 1 : 0), v.Visible + (n is CanvasItem ci && ci.IsVisibleInTree() ? 1 : 0));
            foreach (var c in n.GetChildren()) Walk(c);
        }
        Walk(GetTree().Root);
        var meshes = new Dictionary<string, (int Count, int Surfaces, int Verts, int Shadow)>();
        void Meshes(Node n)
        {
            if (n is MeshInstance3D mi && mi.Mesh is not null)
            {
                var path = n.GetPath().ToString().Split('/'); var key = string.Join('/', path.Take(Math.Min(path.Length - 1, 5))).Replace("@", "");
                var verts = 0; for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++) verts += mi.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
                meshes.TryGetValue(key, out var v); meshes[key] = (v.Count + 1, v.Surfaces + mi.Mesh.GetSurfaceCount(), v.Verts + verts, v.Shadow + (mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off ? 1 : 0));
            }
            foreach (var c in n.GetChildren()) Meshes(c);
        }
        Meshes(GetTree().Root);
        foreach (var kv in meshes.OrderByDescending(k => k.Value.Count).Take(15)) GD.Print($"PERF_MESH {kv.Key} instances={kv.Value.Count} surfaces={kv.Value.Surfaces} verts={kv.Value.Verts} castShadow={kv.Value.Shadow}");
        foreach (var kv in all.OrderByDescending(k => k.Value.Nodes).Take(18)) GD.Print($"PERF_NODES {kv.Key} nodes={kv.Value.Nodes} processing={kv.Value.Processing} visible={kv.Value.Visible}");
        foreach (var kv in all.Where(k => k.Value.Processing > 0).OrderByDescending(k => k.Value.Processing).Take(25)) GD.Print($"PERF_PROCESSING {kv.Key} processing={kv.Value.Processing} of {kv.Value.Nodes}");
    }

    /// <summary>Lists the largest distinct textures reachable from live nodes (width x height x 4 bytes, plus a third for mipmaps when present).</summary>
    private void DumpPerfTextures()
    {
        var seen = new Dictionary<ulong, (long Bytes, string Where, string Size, string Source)>(); long nodes = 0;
        void Walk(Node n)
        {
            nodes++;
            foreach (var prop in n.GetPropertyList())
            {
                if ((prop["type"].AsInt32() != (int)Variant.Type.Object)) continue;
                var name = prop["name"].AsString(); Variant value;
                try { value = n.Get(name); } catch { continue; }
                if (value.VariantType != Variant.Type.Object || value.AsGodotObject() is not Texture2D tex || seen.ContainsKey(tex.GetInstanceId())) continue;
                var bytes = (long)tex.GetWidth() * tex.GetHeight() * 4;
                seen[tex.GetInstanceId()] = (bytes, $"{n.GetPath()}.{name}", $"{tex.GetWidth()}x{tex.GetHeight()}", tex.ResourcePath);
            }
            foreach (var c in n.GetChildren()) Walk(c);
        }
        Walk(GetTree().Root);
        // Textures held by 3D materials (standard and shader parameters) are not node properties.
        void Material(Material? m, string where)
        {
            if (m is null) return;
            void Add(Texture2D? tex, string slot)
            {
                if (tex is null || seen.ContainsKey(tex.GetInstanceId())) return;
                seen[tex.GetInstanceId()] = ((long)tex.GetWidth() * tex.GetHeight() * 4, $"{where}.{slot}", $"{tex.GetWidth()}x{tex.GetHeight()}", tex.ResourcePath);
            }
            if (m is BaseMaterial3D b)
                foreach (BaseMaterial3D.TextureParam t in Enum.GetValues(typeof(BaseMaterial3D.TextureParam))) Add(b.GetTexture(t), t.ToString());
            else if (m is ShaderMaterial sm && sm.Shader is not null)
                foreach (var u in sm.Shader.GetShaderUniformList()) { var uniform = u.AsGodotDictionary()["name"].AsString(); var v = sm.GetShaderParameter(uniform); if (v.VariantType == Variant.Type.Object && v.AsGodotObject() is Texture2D t) Add(t, uniform); }
        }
        void Walk3D(Node n)
        {
            if (n is GeometryInstance3D g)
            {
                Material(g.MaterialOverride, n.Name); Material(g.MaterialOverlay, n.Name);
                if (n is MeshInstance3D mi && mi.Mesh is not null) for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++) Material(mi.GetActiveMaterial(i), n.Name);
            }
            foreach (var c in n.GetChildren()) Walk3D(c);
        }
        Walk3D(GetTree().Root);
        GD.Print($"PERF_TEX distinct={seen.Count} total_mb={seen.Values.Sum(v => v.Bytes) / 1048576.0:0} nodes_walked={nodes}");
        foreach (var v in seen.Values.OrderByDescending(v => v.Bytes).Take(40)) GD.Print($"PERF_TEX {v.Bytes / 1048576.0:0.0}MB {v.Size} {v.Where} src={v.Source}");
    }
}
