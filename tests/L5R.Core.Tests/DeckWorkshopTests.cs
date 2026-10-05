using L5R.Core;

internal static class DeckWorkshopTests
{
    private static readonly DeckCardInfo[] Pool = [
        new("s", "Castle", ["Stronghold"], [], [], true), new("s2", "Other Castle", ["Stronghold"], [], [], true),
        new("b", "Border Keep", ["Holding"], ["Dynasty"], [], true), new("bx", "Border Keep - exp", ["Holding"], ["Dynasty"], [], false),
        new("h", "Bamboo Harvesters", ["Holding"], ["Dynasty"], [], true),
        new("p", "Samurai", ["Personality"], ["Dynasty"], [], true), new("u", "Unique Samurai", ["Personality"], ["Dynasty"], ["Unique"], true),
        new("f", "Strategy", ["Strategy"], ["Fate"], [], true), new("r", "Unwired", ["Spell"], ["Fate"], [], false),
        new("p3", "Bayushi Paneki", ["Personality"], ["Dynasty"], ["Experienced 3"], true),
        new("p5", "Bayushi Paneki", ["Personality"], ["Dynasty"], ["Experienced 5"], true),
        new("strategy-exp1", "Ancestral Lesson", ["Strategy"], ["Fate"], ["Experienced"], true),
        new("strategy-exp2", "Ancestral Lesson", ["Strategy"], ["Fate"], ["Experienced 2"], true)];
    private static void Check(bool value){if(!value)throw new Exception("Deck workshop assertion failed.");}
    private static void Throws(Action action){try{action();}catch(FormatException){return;}throw new Exception("Expected format rejection.");}
    public static void Run(TestRunner runner)
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
            ("Same-titled Experienced levels remain separately selectable and countable",()=>{var d=new DeckWorkshop(Pool);d.Adjust("p3",3);d.Adjust("p5",2);var entries=d.ToDeck().Entries;Check(d.Count("p3")==3&&d.Count("p5")==2&&entries.Any(e=>e.Name=="Bayushi Paneki Experienced 3"&&e.Count==3)&&entries.Any(e=>e.Name=="Bayushi Paneki Experienced 5"&&e.Count==2));}),
            ("Experienced on a non-Personality only separates deck identities",()=>{var d=new DeckWorkshop(Pool);d.Adjust("strategy-exp1",3);d.Adjust("strategy-exp2",3);var entries=d.ToDeck().Entries;Check(entries.Any(e=>e.Name=="Ancestral Lesson Experienced"&&e.Count==3)&&entries.Any(e=>e.Name=="Ancestral Lesson Experienced 2"&&e.Count==3));}),
            ("Troubleshooter suggests the nearest card for Sun and Moon style names",()=>{
                var pool=new DeckCardInfo[]{new("s","Castle",["Stronghold"],[],[],true),new("fk","Fukuzo",["Personality"],["Dynasty"],["Experienced Chuda Fukuzo"],true),
                    new("ax","Utaku Liu Xeung",["Personality"],["Dynasty"],["Experienced"],true),new("a1","Utaku Liu Xeung",["Personality"],["Dynasty"],[],true),new("ay","Daigotsu Aya",["Personality"],["Dynasty"],["Experienced"],true),new("o","Utaku Mai",["Personality"],["Dynasty"],[],true)};
                var d=new DeckWorkshop(pool);
                var t=d.Troubleshoot("Legality: Ivory\nCastle\nFukuzo - expFukuzo\n2x Utaku Liu Xueng - exp\nDaigotsu Aya - expExperienced\nZzzz Qqqq\n");
                var p=t.Problems;
                Check(t.LegalityNeedsFixing&&t.RecognisedLines==1&&p.Length==4);
                Check(p[0].Suggestions[0].DeckName=="Fukuzo");
                Check(p[1].Count==2&&p[1].Suggestions[0].DeckName=="Utaku Liu Xeung Experienced");
                Check(p[2].Suggestions[0].DeckName=="Daigotsu Aya Experienced");
                Check(p[3].Suggestions.Length==0);
                var fixedText=t.Rebuild(x=>x.Suggestions.Length>0?x.Suggestions[0].DeckName:null);
                d.Load(fixedText);
                Check(d.Count("Fukuzo")==1&&d.Count("Utaku Liu Xeung Experienced")==2&&d.Count("Zzzz Qqqq")==0);}),
            ("Troubleshooter leaves the draft unchanged and does not guess across unrelated cards",()=>{var d=new DeckWorkshop(Pool);d.Adjust("Samurai",3);var t=d.Troubleshoot("Legality: Emperor\n3 Strategy\nOther Castlle\n");
                Check(d.Count("Samurai")==3&&t.Problems.Length==1&&t.Problems[0].Suggestions[0].DeckName=="Other Castle"&&!t.LegalityNeedsFixing);}),
            ("Troubleshooter finds the real catalogue cards behind the known Sun and Moon deck errors",()=>{
                var cards=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Batch336Support.RepoRoot(),"content/deck-pool.json"))).RootElement.GetProperty("cards");
                var real=cards.EnumerateArray().Select(c=>new DeckCardInfo(c.GetProperty("id").GetString()!,c.GetProperty("name").GetString()!,c.GetProperty("types").EnumerateArray().Select(x=>x.GetString()!).ToArray(),
                    c.GetProperty("deckTypes").EnumerateArray().Select(x=>x.GetString()!).ToArray(),c.GetProperty("keywords").EnumerateArray().Select(x=>x.GetString()!).ToArray(),true)).ToArray();
                var t=new DeckWorkshop(real).Troubleshoot("Legality: Emperor\nFukuzo - expFukuzo\nTaishuu - expTaishuu\nDaigotsu Aya - expExperienced\nUtaku Liu Xeung - exp\nMakito - exp\n");
                var best=t.Problems.Select(p=>p.Suggestions.Select(s=>s.DeckName).FirstOrDefault()??"").ToArray();
                Check(best[0]=="Fukuzo"&&best[1]=="Taishuu"&&best[2].StartsWith("Daigotsu Aya")&&best[3].StartsWith("Utaku Liu"));
                Check(t.Problems.All(p=>p.Suggestions.Length>0));}),
        ];
        runner.Run("deck-workshop",tests);
    }
}
