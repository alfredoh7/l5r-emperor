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
        if (!OS.GetCmdlineUserArgs().Contains("--perf-vsync")) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        if (scenario != "menu") { _session.StartPractice(false, true, 1); EnterTable(); }
        _perfCpu = Process.GetCurrentProcess().TotalProcessorTime; _perfWall = Stopwatch.GetTimestamp(); _perfAlloc = GC.GetTotalAllocatedBytes();
        for (var g = 0; g < 3; g++) _perfGc[g] = GC.CollectionCount(g);
        GD.Print($"PERF_START scenario={scenario} renderer={RenderingServer.GetCurrentRenderingMethod()} adapter={RenderingServer.GetVideoAdapterName()}");
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
                case "lights" when n is OmniLight3D o: o.Visible = false; break;
                case "hall" when n is FortressHall h: h.Visible = false; break;
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
        GD.Print($"PERF_TEX distinct={seen.Count} total_mb={seen.Values.Sum(v => v.Bytes) / 1048576.0:0} nodes_walked={nodes}");
        foreach (var v in seen.Values.OrderByDescending(v => v.Bytes).Take(25)) GD.Print($"PERF_TEX {v.Bytes / 1048576.0:0.0}MB {v.Size} {v.Where} src={v.Source}");
    }
}
