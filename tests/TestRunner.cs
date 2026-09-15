// Shared by the existing console runners; no test framework or discovery dependency.
internal sealed class TestRunner
{
    private readonly List<string> filters = [];
    private readonly bool list;
    private int selected, failed;
    public bool Valid { get; } = true;

    public TestRunner(string[] args)
    {
        for(var i=0;i<args.Length;i++)
        {
            if(args[i]=="--list") list=true;
            else if(args[i]=="--filter" && i+1<args.Length && !string.IsNullOrWhiteSpace(args[i+1]) && !args[i+1].StartsWith("--"))
                filters.Add(args[++i]);
            else
            {
                Console.Error.WriteLine("Usage: [--list] [--filter TEXT ...] (case-insensitive; repeated filters match any)");
                Valid=false;
                break;
            }
        }
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
            selected++; count++;
            if(list) { Console.WriteLine(label); continue; }
            try { test(); Console.WriteLine("PASS "+label); }
            catch(Exception ex) { failed++; Console.Error.WriteLine($"FAIL {label}\n{ex}"); }
        }
        if(!list && count>0) Console.WriteLine($"{count-(failed-failuresBefore)}/{count} {suite} tests passed.");
    }

    public int Finish()
    {
        if(selected==0) { Console.Error.WriteLine("No tests matched."); return 2; }
        return failed==0?0:1;
    }
}
