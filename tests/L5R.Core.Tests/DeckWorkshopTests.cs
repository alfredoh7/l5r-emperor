using L5R.Core;

internal static class DeckWorkshopTests
{
    private static readonly DeckCardInfo[] Pool = [
        new("s", "Castle", ["Stronghold"], [], [], true), new("s2", "Other Castle", ["Stronghold"], [], [], true),
        new("b", "Border Keep", ["Holding"], ["Dynasty"], [], true), new("bx", "Border Keep - exp", ["Holding"], ["Dynasty"], [], false),
        new("h", "Bamboo Harvesters", ["Holding"], ["Dynasty"], [], true),
        new("p", "Samurai", ["Personality"], ["Dynasty"], [], true), new("u", "Unique Samurai", ["Personality"], ["Dynasty"], ["Unique"], true),
        new("f", "Strategy", ["Strategy"], ["Fate"], [], true), new("r", "Unwired", ["Spell"], ["Fate"], [], false)];
    private static void Check(bool value){if(!value)throw new Exception("Deck workshop assertion failed.");}
    private static void Throws(Action action){try{action();}catch(FormatException){return;}throw new Exception("Expected format rejection.");}
    public static int Run()
    {
        (string Name,Action Test)[] tests=[
            ("Setup selections replace versions and stay out of deck totals",()=>{var d=new DeckWorkshop(Pool);d.SelectSetup("Castle");d.SelectSetup("Other Castle");d.SelectSetup("Border Keep");d.SelectSetup("Border Keep - exp");d.SelectSetup("Bamboo Harvesters");d.Adjust("Samurai",3);Check(d.Count("Castle")==0&&d.Count("Border Keep")==0&&d.Review().Dynasty==3&&d.Review().Unimplemented==1);}),
            ("Incomplete and over-limit drafts round trip without hard blocking",()=>{var d=new DeckWorkshop(Pool);d.Adjust("Samurai",4);d.Adjust("Unique Samurai",2);var q=new DeckWorkshop(Pool);q.Load(DeckFormat.Write(d.ToDeck()));Check(q.Count("Samurai")==4&&q.Review().OverLimit.Length==2);}),
            ("Duplicate and case-variant import lines cannot evade limits",()=>{var d=new DeckWorkshop(Pool);d.Load("Legality: Emperor\n2 Samurai\n2 samurai\n");Check(d.Count("Samurai")==4&&d.Review().OverLimit.Length==1);}),
            ("Failed import preserves entire open draft",()=>{var d=new DeckWorkshop(Pool);d.Adjust("Samurai",3);foreach(var t in new[]{"3 Strategy", "Legality: Modern\n3 Strategy", "Legality: Emperor\n3 Strategy\nBogus", "Legality: Emperor\n-1 Samurai", "Legality: Emperor\n1000 Samurai"}){Throws(()=>d.Load(t));Check(d.Count("Samurai")==3&&d.Count("Strategy")==0);}}),
            ("Malformed multiple setup choices are retained and flagged",()=>{var d=new DeckWorkshop(Pool);d.Load("Legality: Emperor\nCastle\nOther Castle\nBorder Keep\nBamboo Harvesters");Check(d.Count("Castle")==1&&d.Count("Other Castle")==1&&d.Review().Warnings.Any(w=>w.Contains("exactly one Stronghold")));}),
            ("Saving is atomic and existing files require overwrite",()=>{var root=Path.Combine(Path.GetTempPath(),"l5r-decks-"+Guid.NewGuid());try{var s=new DeckFileStore(root);var d=new DeckWorkshop(Pool);d.Adjust("Samurai",3);var path=s.Save("Test deck",d.ToDeck());var before=DeckFileStore.Read(path);d.Adjust("Samurai",1);try{s.Save("Test deck",d.ToDeck());throw new Exception("Overwrite unexpectedly allowed");}catch(IOException){}Check(DeckFileStore.Read(path)==before&&Directory.GetFiles(root).Length==1);s.Save("Test deck",d.ToDeck(),true);d.Load(DeckFileStore.Read(path));Check(d.Count("Samurai")==4);}finally{Directory.Delete(root,true);}}),
            ("Deck filenames reject traversal and Windows reserved names",()=>{var s=new DeckFileStore(Path.GetTempPath());foreach(var n in new[]{"../secret","a/b","CON","bad\nname",""}){try{s.PathFor(n);throw new Exception("Unsafe name accepted");}catch(ArgumentException){}}}),
            ("Unimplemented cards are explicit even in complete draft",()=>{var d=new DeckWorkshop(Pool);d.Adjust("Samurai",40);d.Adjust("Unwired",40);Check(d.Review().Unimplemented==1&&!d.Review().ChecksPassed);}),
        ];
        var failed=0;foreach(var (name,test) in tests){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.Error.WriteLine("FAIL "+name+"\n"+e);}}Console.WriteLine($"{tests.Length-failed}/{tests.Length} deck workshop tests passed.");return failed;
    }
}
