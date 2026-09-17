using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    // Explicit screenshot-only public-view fixture. This does not run in ordinary matches,
    // edit the engine, or imply that a pictured sequence was played through legally.
    private void PreparePersistentHomeCapture()
    {
        if (_capturePath is null || !_captureMode.StartsWith("published-home", StringComparison.Ordinal)) return;
        _autoOpponent = false; _reducedMotion = true;
        _session.StartPractice(false, true, 0);
        var snapshot = _session.Snapshot!;
        CardView PublicCard(string id, string key, int owner, Zone zone)
        {
            var def = CardCatalog.Get(key);
            return new CardView(id, def, owner, zone, true, false, null, null, def.Force, def.Chi,
                UnitForce: def.Type == CardType.Personality ? def.Force : null);
        }
        var players = snapshot.View.Players.Select(p =>
        {
            var units = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Type == CardType.Personality && c.Clan == p.Clan)
                .Take(3).Select((d,i) => PublicCard($"home-preview-unit-{p.Id}-{i}", d.Id, p.Id, Zone.Home));
            var home = p.Home.Concat(units).ToList();
            if (p.Id == 0)
            {
                home.Add(PublicCard("home-preview-air", "oracle-l5r-6333", p.Id, Zone.Home));
                home.Add(PublicCard("home-preview-water", "oracle-l5r-6339", p.Id, Zone.Home));
            }
            return p with { Home = home.ToArray(), Army = [], EventsInPlay = p.Id == 0
                ? [PublicCard("home-preview-suspicions", "oracle-l5r-7675", p.Id, Zone.EventInPlay)] : [] };
        }).ToArray();
        var view = snapshot.View with { Players = players, FavorPlayerId = 0, ActivePlayerId = 0, PriorityPlayerId = 0,
            ViewerId = 0, Phase = Phase.Action, PendingChoice = null, CurrentBattleProvince = null, UnresolvedBattlefields = [] };
        typeof(MatchSession).GetProperty(nameof(MatchSession.Snapshot))!.SetValue(_session,
            snapshot with { View = view, Actions = [], Mode = "Native artwork review" });
        _uiQueue.Clear(); _selected = null; _chosen = null; _selectedHomeReminder = null;
        _tableLayoutSeen = false; Render(); ResetTableCamera();
        if (_captureMode.EndsWith("detail", StringComparison.Ordinal))
        {
            var bounds = _persistentBounds[0].Grow(70);
            _cameraCenter = bounds.GetCenter(); _cameraZoom = FitRegion(bounds); _cameraFitted = false;
            ApplyTableCamera();
        }
        SetNotice("Native artwork preview · staged public board state");
        GD.Print("HOME_PREVIEW: native renderer with a staged public snapshot; not a gameplay acceptance test.");
    }
}
