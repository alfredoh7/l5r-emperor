// Shared by the existing console runners; no test framework or discovery dependency.
using System.Diagnostics;

internal sealed class TestRunner
{
    private const string Usage = "Usage: [--list] [--filter TEXT ...] [--shard I/N] (filters are case-insensitive; repeated filters match any; --shard runs the I-th of N deterministic slices of the selected cases)";
    private readonly List<string> filters = [];
    private readonly bool list;
    private readonly int shardIndex, shardCount = 1;
    private readonly List<(double Seconds, string Label)> timings = [];
    private readonly List<string> failures = [];
    private int selected, failed, position;
    public bool Valid { get; } = true;

    public TestRunner(string[] args)
    {
        for(var i=0;i<args.Length;i++)
        {
            if(args[i]=="--list") list=true;
            else if(args[i]=="--filter" && i+1<args.Length && !string.IsNullOrWhiteSpace(args[i+1]) && !args[i+1].StartsWith("--"))
                filters.Add(args[++i]);
            else if(args[i]=="--shard" && i+1<args.Length && TryParseShard(args[++i],out shardIndex,out shardCount)) { }
            else
            {
                Console.Error.WriteLine(Usage);
                Valid=false;
                break;
            }
        }
    }

    // "2/4" is the second of four slices; 1-based on the command line so a CI matrix reads naturally.
    private static bool TryParseShard(string text, out int index, out int count)
    {
        index=0; count=1;
        var parts=text.Split('/');
        if(parts.Length!=2 || !int.TryParse(parts[0],out var one) || !int.TryParse(parts[1],out var n) || n<1 || one<1 || one>n) return false;
        index=one-1; count=n;
        return true;
    }

    public void Run(string suite, IEnumerable<(string Name, Action Test)> tests)
    {
        var count=0; var failuresBefore=failed;
        foreach(var (name,test) in tests)
        {
            var method=test.Method.Name;
            // Top-level C# methods have generated wrappers; expose the source method name.
            if(method.Contains("g__")) method=method.Split("g__")[1].Split('|')[0];
            var label=method.Contains('<')?$"{suite}/{name}":$"{suite}/{method}: {name}";
            if(filters.Count>0 && !filters.Any(f=>label.Contains(f,StringComparison.OrdinalIgnoreCase))) continue;
            // Round-robin over the cases that passed the filter, in registration order: every shard of one build sees the same order,
            // so the slices partition the run exactly, and neighbouring (often equally heavy) cases land in different shards.
            if(position++ % shardCount != shardIndex) continue;
            selected++; count++;
            if(list) { Console.WriteLine(label); continue; }
            var clock=Stopwatch.StartNew();
            try { test(); Console.WriteLine("PASS "+label); }
            catch(Exception ex) { failed++; failures.Add(label); Console.Error.WriteLine($"FAIL {label}\n{ex}"); }
            timings.Add((clock.Elapsed.TotalSeconds,label));
        }
        if(!list && count>0) Console.WriteLine($"{count-(failed-failuresBefore)}/{count} {suite} tests passed.");
    }

    public int Finish()
    {
        if(selected==0) { Console.Error.WriteLine("No tests matched."); return 2; }
        if(!list) Summarize();
        return failed==0?0:1;
    }

    // One block at the end: the slowest cases (to see where a shard's time goes) and, when anything failed, every failing label
    // together, so a red run does not need the whole log scrolled for FAIL lines. Also appended to the Actions job summary when set.
    private void Summarize()
    {
        var shard=shardCount>1?$" (shard {shardIndex+1}/{shardCount})":"";
        var lines=new List<string> { $"{selected-failed}/{selected} cases passed{shard}, {timings.Sum(t=>t.Seconds):0.0}s in cases." };
        if(failures.Count>0) { lines.Add("FAILED:"); lines.AddRange(failures.Select(f=>"  "+f)); }
        lines.Add("Slowest cases:");
        lines.AddRange(timings.OrderByDescending(t=>t.Seconds).Take(8).Select(t=>$"  {t.Seconds,6:0.0}s {Trim(t.Label)}"));
        foreach(var line in lines) Console.WriteLine(line);
        var summary=Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if(string.IsNullOrEmpty(summary)) return;
        try { File.AppendAllText(summary,"```\n"+string.Join('\n',lines)+"\n```\n"); } catch(IOException) { } catch(UnauthorizedAccessException) { }
    }

    private static string Trim(string label) => label.Length>140?label[..140]+"...":label;
}
