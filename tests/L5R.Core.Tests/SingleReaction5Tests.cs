using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #833, single-reaction slice 5: cards that print exactly one Reaction. Each test opens the real trigger window through ordinary engine play (a purchase, an
// equip, a Battle action targeting a card, an Engage, a battle's resolution ...), proves the Reaction is offered enabled to its own player only (and not to the
// other), accepted with the printed effect checked against the card text, refused once the window is closed or never opened, and (where it matters) not offered
// for an action the text does not cover. Where the rig is symmetrical both seats are run (player 0 and player 1 as the reacting player).
internal static partial class SingleReaction5Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }

    private const string Bow = "training-bow-at-battle", Destroy = "training-destroy-no-attachments", Bower = "training-battle-bower", Crane = "training-crane-samurai", Lion = "training-lion-samurai";

    // Declines every open reaction window (only those of `type` when given: an action that goes on to ask its own question is left at that question).
    private static void Settle(MatchEngine e, string? type = null)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null && (type is null || e.State.PendingChoice.Type == type); guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner, listed for nobody else, refused if the other player submits it; on a copy where the
    // window is declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`).
    private static GameCommand InWindow(MatchEngine e, string windowType, int owner, string ability, string cardId, string? performerId = null, string? targetId = null)
    {
        Check(e.State.PendingChoice?.Type == windowType || windowType == "entryReaction" && e.State.PendingChoice?.Type == "proclaim", ability + ": the window is " + e.State.PendingChoice?.Type);
        if (e.State.PriorityPlayerId != owner && e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the other player holds the first opportunity and has nothing to react with
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner}" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        var command = targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
        Check(!CanUse(e, 1 - owner, ability, cardId), ability + " is listed enabled for the other player");
        Reject(e, command with { PlayerId = 1 - owner });
        var closed = e.Clone();
        Settle(closed, windowType == "entryReaction" ? null : windowType);
        Check(closed.State.PendingChoice?.Type != windowType, ability + ": the window did not close");
        Check(!CanUse(closed, owner, ability, cardId, performerId), ability + " is still offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        return command;
    }

    // A fresh position in which the card sits in play (or in hand) and no window was ever opened: not offered, and its command is refused.
    private static void NeverOpened(MatchEngine e, int owner, string ability, string cardId, GameCommand command)
    {
        Check(e.State.PendingChoice is null, "a window is open");
        Check(!CanUse(e, owner, ability, cardId) && !CanUse(e, 1 - owner, ability, cardId), ability + " is offered with no window open");
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = owner });
    }

    // Player `actor` performs `ability` from `cardId` on `targetId` in the position `s`; the engine is returned with that action's window open.
    private static MatchEngine Open(MatchState s, int actor, string ability, string cardId, string targetId, string? performerId = null)
    {
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, actor, ability, cardId, targetId, performerId));
        return e;
    }

    // ---- entering-play windows ----------------------------------------------------------------------------------------------------------------------------------------------
    // A card bought from player 0's province 0 in the Dynasty Phase (a Personality is recruited, a Follower is not: see BuyFollower), leaving its entering-play window open.
    private static (MatchEngine E, string Id) Buy(string def, Action<MatchState>? tweak = null, int gold = 14, string clan = "Dragon")
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = clan; s.Players[0].Honor = 30;
        Gold(s, 0, gold);
        var entering = s.Cards[s.Players[0].Provinces[0].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false });
        Accept(e, option.Command!);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"{def} bought, but the window is {e.State.PendingChoice?.Type}");
        return (e, entering.Id);
    }

    // The same Personality standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, out CardInstance card, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        card = Add(s, def, 0, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    // ---- before-action windows: another player's action targets one of this player's cards ---------------------------------------------------------------------------------------
    private static void QamarArmor()
    {
        const string ability = "pe-qamar-negate-destruction";
        var def = Def(ability);
        const string daikyu = "oracle-l5r-8823";
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // The wearer (a Crab, Force 3 + the Armor's 4, lowered to 1) is hit by a Ranged 5 Attack, a destruction. `target` false: a plain bow instead.
            MatchEngine Rig(bool act, bool destroys, out CardInstance wearer, out CardInstance armor, out CardInstance bowItem)
            {
                var s = Table(Phase.Battle, a);
                wearer = Kw(Add(s, Crane, r, Zone.Battlefield, province: 0), "Crab Clan");
                armor = Add(s, def, r, Zone.Attached, province: 0, parent: wearer.Id);
                Force(wearer, -6);
                var striker = Add(s, destroys ? Lion : Bower, a, Zone.Battlefield, province: 0);
                bowItem = destroys ? Add(s, daikyu, a, Zone.Attached, province: 0, parent: striker.Id) : striker;
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, destroys ? Use(e, a, "tsuruchi-daikyu-attack", bowItem.Id, wearer.Id) : Offer(e, a, Bow, striker.Id, wearer.Id));
                return e;
            }
            var e = Rig(true, true, out var wearer, out var armor, out var bow);
            Eq(e.CardForceForTest(wearer.Id), 1, "fixture: Force 3 + 4 - 6");
            var command = InWindow(e, "attackReaction", r, ability, armor.Id);
            Accept(e, command); Settle(e);
            Eq(e.State.Cards[wearer.Id].Zone, Zone.Battlefield, $"seat {r}: his destruction from the Attack was negated");
            Eq(e.State.Cards[armor.Id].Zone, Zone.Attached, "and the Armor stays on him");
            Check(e.State.Cards[bow.Id].Bowed, "the Attack was still taken and its cost paid");
            var control = Rig(true, true, out var w2, out _, out _); Settle(control);
            Eq(control.State.Cards[w2.Id].Zone, Zone.Dead, "declined, the Crab is destroyed");
            // An action that cannot destroy him (a bow) does not open the Reaction.
            var bowed = Rig(true, false, out _, out var a3, out _);
            Check(!CanUse(bowed, r, ability, a3.Id), "a bow action cannot destroy him: not offered");
            var quiet = Rig(false, true, out _, out var a4, out _);
            NeverOpened(quiet, r, ability, a4.Id, command with { CardId = a4.Id });
        }
    }

    private static void WakuiRedirect()
    {
        const string ability = "wlpt-wakui-redirect";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool magistrate, bool wakuiHome, out CardInstance wakui, out CardInstance target, bool bowedWakui = false)
            {
                var s = Table(Phase.Battle, a);
                wakui = wakuiHome ? Add(s, def, r, Zone.Home, bowedWakui) : Add(s, def, r, Zone.Battlefield, bowedWakui, 0);
                target = Add(s, Crane, r, Zone.Battlefield, province: 0);
                if (magistrate) Kw(target, "Magistrate");
                var bower = Add(s, Bower, a, Zone.Battlefield, province: 0);
                return act ? Open(s, a, Bow, bower.Id, target.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, true, false, out var wakui, out var magistrate);
            var command = InWindow(e, "beforeActionReaction", r, ability, wakui.Id);
            Accept(e, command); Settle(e);
            Check(e.State.Cards[wakui.Id].Bowed && !e.State.Cards[magistrate.Id].Bowed, $"seat {r}: the action targets Wakui instead: he is bowed, the Magistrate is not");
            var control = Rig(true, true, false, out var w2, out var m2); Settle(control);
            Check(control.State.Cards[m2.Id].Bowed && !control.State.Cards[w2.Id].Bowed, "declined, the Magistrate is bowed");
            var plain = Rig(true, false, false, out var w3, out _);
            Check(!CanUse(plain, r, ability, w3.Id), "a Personality who is no Magistrate is not covered");
            var home = Rig(true, true, true, out var w4, out _);
            Check(!CanUse(home, r, ability, w4.Id), "Wakui at home has no battlefield");
            var bowedWakui = Rig(true, true, false, out var w5, out _, true);
            Check(!CanUse(bowedWakui, r, ability, w5.Id), "a bowed Wakui takes no Reaction");
            var quiet = Rig(false, true, false, out var w6, out _);
            NeverOpened(quiet, r, ability, w6.Id, command with { CardId = w6.Id });
        }
    }

    private static void BrothersOfGoemon()
    {
        const string ability = "brothers-goemon-negate";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, int? ringOwner, bool targetHost, out CardInstance brothers, out CardInstance host, out CardInstance other)
            {
                var s = Table(Phase.Battle, a);
                host = Add(s, Crane, r, Zone.Battlefield, province: 0);
                brothers = Add(s, def, r, Zone.Attached, province: 0, parent: host.Id);
                other = Add(s, Lion, r, Zone.Battlefield, province: 0);
                if (ringOwner is { } o) Add(s, "training-ring-of-air", o, Zone.Home);
                var bower = Add(s, Bower, a, Zone.Battlefield, province: 0);
                return act ? Open(s, a, Bow, bower.Id, targetHost ? host.Id : other.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, r, true, out var brothers, out var host, out _);
            var command = InWindow(e, "beforeActionReaction", r, ability, brothers.Id);
            Accept(e, command); Settle(e);
            Check(!e.State.Cards[host.Id].Bowed, $"seat {r}: the bowing of the unit's card was negated");
            var control = Rig(true, r, true, out _, out var h2, out _); Settle(control);
            Check(control.State.Cards[h2.Id].Bowed, "declined, the bow lands");
            var noRing = Rig(true, null, true, out var b3, out _, out _);
            Check(!CanUse(noRing, r, ability, b3.Id), "with no Ring controlled it is not offered");
            var foeRing = Rig(true, a, true, out var b4, out _, out _);
            Check(!CanUse(foeRing, r, ability, b4.Id), "a Ring the other player controls does not count");
            var elsewhere = Rig(true, r, false, out var b5, out _, out _);
            Check(!CanUse(elsewhere, r, ability, b5.Id), "an action on a card outside the unit is not answered");
            var quiet = Rig(false, r, true, out var b6, out _, out _);
            NeverOpened(quiet, r, ability, b6.Id, command with { CardId = b6.Id });
        }
    }

    private static void ChugoSeido()
    {
        const string ability = "chugo-seido-redirect";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool bowedChugo, bool otherSamurai, out CardInstance chugo, out CardInstance target, out CardInstance here, out CardInstance home)
            {
                var s = Table(Phase.Battle, a);
                chugo = Add(s, def, r, Zone.Home, bowedChugo);
                target = Add(s, "training-samurai", r, Zone.Battlefield, province: 0);
                here = Add(s, "training-samurai", r, Zone.Battlefield, province: 0);
                if (!otherSamurai) { s.Cards.Remove(here.Id); }
                Add(s, Crane, r, Zone.Battlefield, province: 0); // not a Samurai
                home = Add(s, "training-samurai", r, Zone.Home); // a Samurai at another location
                var bower = Add(s, Bower, a, Zone.Battlefield, province: 0);
                return act ? Open(s, a, Bow, bower.Id, target.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, false, true, out var chugo, out var target, out var here, out var home);
            var command = InWindow(e, "beforeActionReaction", r, ability, chugo.Id, null, here.Id);
            var offers = Options(e, r, ability, chugo.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
            Check(offers.SequenceEqual([here.Id]), "only the other Samurai at the same location is offered (not the target, the non-Samurai or the one at home): " + string.Join(",", offers) + home.Id);
            Accept(e, command); Settle(e);
            Check(e.State.Cards[here.Id].Bowed && !e.State.Cards[target.Id].Bowed, $"seat {r}: the action landed on the chosen Samurai instead");
            Check(e.State.Cards[chugo.Id].Zone != Zone.Home, "Chugo Seido was destroyed as the cost");
            var control = Rig(true, false, true, out var c2, out var t2, out _, out _); Settle(control);
            Check(control.State.Cards[t2.Id].Bowed && control.State.Cards[c2.Id].Zone == Zone.Home, "declined, the target is bowed and the Holding stays");
            var none = Rig(true, false, false, out var c3, out _, out _, out _);
            Check(!CanUse(none, r, ability, c3.Id), "with no other Samurai at the location there is nothing to choose");
            var bowed = Rig(true, true, true, out var c4, out _, out _, out _);
            Check(!CanUse(bowed, r, ability, c4.Id), "a bowed Holding is not offered (the cost is its destruction, but the Reaction needs it unbowed)" );
            var quiet = Rig(false, false, true, out var c5, out _, out var h5, out _);
            NeverOpened(quiet, r, ability, c5.Id, command with { CardId = c5.Id, TargetId = h5.Id });
        }
    }

    private static void HallsOfTheForgotten()
    {
        const string ability = "halls-forgotten-delay";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, string targetDef, out string halls, out CardInstance target)
            {
                var s = Table(Phase.Battle, a);
                halls = s.Players[r].StrongholdId; s.Cards[halls].DefinitionId = def;
                target = Add(s, targetDef, r, Zone.Battlefield, province: 0);
                Force(target, 10);
                var killer = Add(s, "training-no-attachments-destroyer", a, Zone.Battlefield, province: 0);
                return act ? Open(s, a, Destroy, killer.Id, target.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, "training-berserker", out var halls, out var berserker);
            var command = InWindow(e, "beforeActionReaction", r, ability, halls);
            Accept(e, command); Settle(e);
            Eq(e.State.Cards[berserker.Id].Zone, Zone.Battlefield, $"seat {r}: he is not destroyed when the action resolves");
            var delayed = e.State.DelayedEffects.Single();
            Check(delayed.Kind == DelayedKinds.Destroy && delayed.CardId == berserker.Id && delayed.Until == DelayUntil.EndOfBattle, "a destruction of the Berserker waits for the end of the battle");
            RunUntil(e, () => e.State.Events.Any(x => x.Type == "battleResolved"));
            Eq(e.State.Cards[berserker.Id].Zone, Zone.Dead, "destroyed once the battle has ended");
            Eq(e.State.DelayedEffects.Count, 0, "the delay is spent");
            var control = Rig(true, "training-berserker", out _, out var b2); Settle(control);
            Eq(control.State.Cards[b2.Id].Zone, Zone.Dead, "declined, the Berserker is destroyed at once");
            var plain = Rig(true, Crane, out var h3, out _);
            Check(!CanUse(plain, r, ability, h3), "a Personality who is not a Berserker gives Halls nothing to delay");
            var quiet = Rig(false, "training-berserker", out var h4, out _);
            NeverOpened(quiet, r, ability, h4, command with { CardId = h4 });
            // A non-Battle (Open) action does not open Halls' Reaction.
            var s = Table(Phase.Action, a); s.PriorityPlayerId = a;
            var stronghold = s.Players[r].StrongholdId; s.Cards[stronghold].DefinitionId = def;
            var zerk = Add(s, "training-berserker", r, Zone.Home);
            var src = Add(s, "training-ninja-source", a, Zone.Home);
            var open = Open(s, a, "training-bow-any", src.Id, zerk.Id);
            Check(!CanUse(open, r, ability, stronghold), "an Open action is not a Battle action");
        }
    }

    private static void MotoShigeru()
    {
        const string ability = "shigeru-negate";
        var def = Def(ability);
        var tsujiken = Def("tsujiken-exp-dishonor");
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Setup(int others, bool act, out CardInstance shigeru, out CardInstance mine)
            {
                var s = Table(Phase.Action, a); s.PriorityPlayerId = a; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
                shigeru = Add(s, def, r, Zone.Home); mine = Add(s, Crane, r, Zone.Home);
                for (var i = 1; i < others; i++) Add(s, "training-crane-veteran", r, Zone.Home);
                var tsu = Add(s, tsujiken, a, Zone.Home);
                return act ? Open(s, a, "tsujiken-exp-dishonor", tsu.Id, mine.Id) : MatchEngine.FromTestState(s);
            }
            var e = Setup(1, true, out var shigeru, out var mine);
            var honor = Honor(e, r);
            var command = InWindow(e, "beforeActionReaction", r, ability, shigeru.Id);
            Accept(e, command); Settle(e);
            Check(!e.State.Cards[mine.Id].Dishonorable, $"seat {r}: the Limited action's effects were negated, he is not dishonored");
            Eq(Honor(e, r), honor, "and his controller lost no Honor");
            Eq(e.PersonalHonorForTest(shigeru.Id), CardCatalog.Get(def).PersonalHonor - 1, "her Personal Honor was permanently reduced by the one other Personality");
            Check(e.State.Cards[shigeru.Id].Zone == Zone.Home, "with Personal Honor above 0 she stays");
            var two = Setup(2, true, out var s2, out _);
            Accept(two, InWindow(two, "beforeActionReaction", r, ability, s2.Id)); Settle(two);
            Check(two.State.Cards[s2.Id].Zone is Zone.Dead or Zone.FateDiscard or Zone.DynastyDiscard, "two others take her Personal Honor (2) to 0: she commits seppuku");
            var control = Setup(1, true, out _, out var m3); Settle(control);
            Check(control.State.Cards[m3.Id].Dishonorable, "declined, the action dishonors him");
            var quiet = Setup(1, false, out var s4, out _);
            NeverOpened(quiet, r, ability, s4.Id, command with { CardId = s4.Id });
            // A Battle action (not Limited) that targets her controller's Personality is not answered by her.
            var b = Table(Phase.Battle, a);
            var sh = Add(b, def, r, Zone.Home); var target = Add(b, Crane, r, Zone.Battlefield, province: 0);
            var bower = Add(b, Bower, a, Zone.Battlefield, province: 0);
            var be = Open(b, a, Bow, bower.Id, target.Id);
            Check(!CanUse(be, r, ability, sh.Id), "a Battle action does not open her Reaction");
        }
    }

    private static void ChampionOfThunderTroops()
    {
        const string ability = "champion-of-thunder-negate";
        var def = Def(ability, "O-Yoroi Troops");
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // `action`: the Battle action aimed at either the unit's Personality or at the Follower itself ("bow a target enemy card").
            MatchEngine Rig(bool act, int mode, out CardInstance troops, out CardInstance host)
            {
                var s = Table(Phase.Battle, a);
                host = Add(s, Lion, r, Zone.Battlefield, province: 0);
                troops = Add(s, def, r, Zone.Attached, province: 0, parent: host.Id);
                var other = Add(s, Crane, r, Zone.Battlefield, province: 0);
                var bower = Add(s, Bower, a, Zone.Battlefield, province: 0);
                var cardBower = Add(s, "training-card-bower", a, Zone.Battlefield, province: 0);
                if (!act) return MatchEngine.FromTestState(s);
                return mode switch
                {
                    0 => Open(s, a, Bow, bower.Id, host.Id),
                    1 => Open(s, a, "training-bow-enemy-card-at-battle", cardBower.Id, troops.Id),
                    _ => Open(s, a, Bow, bower.Id, other.Id),
                };
            }
            // Personality: the targeted card in the unit is the Personality.
            var e = Rig(true, 0, out var troops, out var host);
            var command = InWindow(e, "beforeActionReaction", r, ability, troops.Id, null, host.Id);
            Accept(e, command); Settle(e);
            Check(!e.State.Cards[host.Id].Bowed, $"seat {r}: the action's effects on the unit's targeted card were negated");
            var control = Rig(true, 0, out _, out var h2); Settle(control);
            Check(control.State.Cards[h2.Id].Bowed, "declined, the bow lands");
            // The Follower itself is the targeted card: "this card".
            var self = Rig(true, 1, out var t3, out _);
            Check(CanUse(self, r, ability, t3.Id), "an action that targets the Follower itself offers the Reaction");
            Accept(self, Options(self, r, ability, t3.Id).First(o => o.Enabled && o.Command!.TargetId == t3.Id).Command!); Settle(self);
            Check(!self.State.Cards[t3.Id].Bowed, "the Follower targeted by the bow is not bowed");
            var elsewhere = Rig(true, 2, out var t4, out _);
            Check(elsewhere.State.PendingChoice is null && !CanUse(elsewhere, r, ability, t4.Id), "an action on a card outside the unit opens nothing");
            var quiet = Rig(false, 0, out var t5, out _);
            NeverOpened(quiet, r, ability, t5.Id, command with { CardId = t5.Id });
        }
    }

    private static void ShibaSansesuke()
    {
        const string ability = "sansesuke-redirect";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, string targetDef, bool sansesukeHome, bool bowedSansesuke, out CardInstance sans, out CardInstance target)
            {
                var s = Table(Phase.Battle, a);
                sans = sansesukeHome ? Add(s, def, r, Zone.Home, bowedSansesuke) : Add(s, def, r, Zone.Battlefield, bowedSansesuke, 0);
                target = Add(s, targetDef, r, Zone.Battlefield, province: 0);
                var killer = Add(s, "training-no-attachments-destroyer", a, Zone.Battlefield, province: 0);
                return act ? Open(s, a, Destroy, killer.Id, target.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, "training-shugenja", false, false, out var sans, out var shugenja);
            var command = InWindow(e, "beforeActionReaction", r, ability, sans.Id);
            Accept(e, command);
            Check(e.State.Cards[sans.Id].Bowed || e.State.Cards[sans.Id].Zone != Zone.Battlefield, "Sansesuke bowed as the cost");
            Settle(e);
            Eq(e.State.Cards[shugenja.Id].Zone, Zone.Battlefield, $"seat {r}: the Shugenja was spared");
            Eq(e.State.Cards[sans.Id].Zone, Zone.Dead, "the action targeted Sansesuke instead and destroyed him");
            var control = Rig(true, "training-shugenja", false, false, out var s2, out var g2); Settle(control);
            Check(control.State.Cards[g2.Id].Zone == Zone.Dead && control.State.Cards[s2.Id].Zone == Zone.Battlefield, "declined, the Shugenja dies and Sansesuke is untouched");
            var samurai = Rig(true, Crane, false, false, out var s3, out _);
            Check(!CanUse(samurai, r, ability, s3.Id), "not for a non-Shugenja");
            var bowed = Rig(true, "training-shugenja", false, true, out var s4, out _);
            Check(!CanUse(bowed, r, ability, s4.Id), "a bowed Sansesuke cannot bow again");
            var home = Rig(true, "training-shugenja", true, false, out var s5, out _);
            Check(!CanUse(home, r, ability, s5.Id), "not at the Shugenja's location");
            var quiet = Rig(false, "training-shugenja", false, false, out var s6, out _);
            NeverOpened(quiet, r, ability, s6.Id, command with { CardId = s6.Id });
        }
    }

    private static void StoneOfRemembrance()
    {
        const string ability = "w829-stone-negate";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool ownAction, out CardInstance follower, out CardInstance stone)
            {
                var s = Table(Phase.Battle, ownAction ? r : a);
                var attacker = Add(s, Lion, a, Zone.Battlefield, province: 0);
                var victim = Add(s, Crane, r, Zone.Battlefield, province: 0);
                follower = Add(s, "training-follower", r, Zone.Attached, province: 0, parent: victim.Id);
                Add(s, "training-item", a, Zone.Attached, province: 0, parent: attacker.Id);
                stone = Add(s, def, r, Zone.Home);
                var card = Add(s, "oracle-l5r-9916", ownAction ? r : a, Zone.Hand);
                return act ? Open(s, ownAction ? r : a, "never-afraid", card.Id, follower.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, false, out var follower, out var stone);
            var command = InWindow(e, "beforeActionReaction", r, ability, stone.Id);
            Accept(e, command);
            Eq(e.State.Cards[stone.Id].Zone, Zone.RemovedFromGame, $"seat {r}: the Stone is removed from the game");
            Check(e.State.Events.Any(ev => ev.Type == "cardTorn" && ev.CardId == stone.Id), "and torn in half");
            Settle(e);
            Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower's destruction was negated");
            var control = Rig(true, false, out var f2, out var s2); Settle(control);
            Check(control.State.Cards[f2.Id].Zone != Zone.Attached && control.State.Cards[s2.Id].Zone == Zone.Home, "declined, the Follower is destroyed and the Stone stays");
            var own = Rig(true, true, out _, out var s3);
            Check(!CanUse(own, r, ability, s3.Id), "your own action does not open the Stone");
            var quiet = Rig(false, false, out _, out var s4);
            NeverOpened(quiet, r, ability, s4.Id, command with { CardId = s4.Id });
            Check(!DeckWorkshop.CountsTowardMinimumOf(CardCatalog.Get(def)), "the Stone does not count toward the minimum deck size");
        }
    }

    private static void CastleOfOrder()
    {
        const string ability = "castle-of-order-redirect";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool battleAction, out string castle, out CardInstance ninjaA, out CardInstance ninjaB)
            {
                var s = Table(battleAction ? Phase.Battle : Phase.Action, a); s.PriorityPlayerId = a;
                castle = s.Players[r].StrongholdId; s.Cards[castle].DefinitionId = def;
                ninjaA = Add(s, "training-ninja-source", r, battleAction ? Zone.Battlefield : Zone.Home, province: battleAction ? 0 : null);
                ninjaB = Add(s, "training-ninja-source", r, battleAction ? Zone.Battlefield : Zone.Home, province: battleAction ? 0 : null);
                if (battleAction) { Add(s, Crane, r, Zone.Battlefield, province: 0); Add(s, "training-ninja-source", r, Zone.Home); }
                var bower = Add(s, battleAction ? Bower : "training-ninja-source", a, battleAction ? Zone.Battlefield : Zone.Home, province: battleAction ? 0 : null);
                var ability2 = battleAction ? Bow : "training-bow-any";
                return act ? Open(s, a, ability2, bower.Id, ninjaA.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, true, out var castle, out var ninjaA, out var ninjaB);
            var command = InWindow(e, "beforeActionReaction", r, ability, castle, null, ninjaB.Id);
            var offers = Options(e, r, ability, castle).Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
            Check(offers.SequenceEqual([ninjaB.Id]), "only the other Ninja at the same location (not the plain Personality or the Ninja at home): " + string.Join(",", offers));
            Accept(e, command); Settle(e);
            Check(e.State.Cards[ninjaB.Id].Bowed && !e.State.Cards[ninjaA.Id].Bowed, $"seat {r}: the action targets the other Ninja instead");
            // Once per battle: the next Battle action against a Ninja is not answered.
            var st = e.State; st.PriorityPlayerId = a; st.Cards[ninjaB.Id].Bowed = false;
            var bower2 = Add(st, Bower, a, Zone.Battlefield, province: 0);
            var again = MatchEngine.FromTestState(st);
            Accept(again, Offer(again, a, Bow, bower2.Id, ninjaA.Id));
            Check(!CanUse(again, r, ability, castle), "used once this battle: not offered again");
            var control = Rig(true, true, out _, out var a2, out _); Settle(control);
            Check(control.State.Cards[a2.Id].Bowed, "declined, the targeted Ninja is bowed");
            // A new battle restores it.
            var later = again.State; later.BattleNumber++; later.PriorityPlayerId = a; later.Cards[ninjaA.Id].Bowed = false;
            var bower3 = Add(later, Bower, a, Zone.Battlefield, province: 0);
            var next = MatchEngine.FromTestState(later);
            Accept(next, Offer(next, a, Bow, bower3.Id, ninjaA.Id));
            Check(CanUse(next, r, ability, castle, null), "usable again in the next battle");
            // Printed "Battle action": an Open action in the Action phase does not open it.
            var open = Rig(true, false, out var c4, out _, out _);
            Check(!CanUse(open, r, ability, c4), "an Open action is not a Battle action");
            var quiet = Rig(false, true, out var c5, out _, out _);
            NeverOpened(quiet, r, ability, c5, command with { CardId = c5 });
        }
    }

    private static void ThunderersProtection()
    {
        const string ability = "wl-thunderers-protection-bow";
        var def = Def(ability);
        const string tigerClaw = "oracle-l5r-10910";
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool attack, bool targetUnit, out CardInstance host, out CardInstance spell, out CardInstance striker)
            {
                var s = Table(Phase.Battle, a);
                host = Add(s, "training-shugenja", r, Zone.Battlefield, province: 0);
                spell = Add(s, def, r, Zone.Attached, province: 0, parent: host.Id);
                var bystander = Add(s, Crane, r, Zone.Battlefield, province: 0);
                striker = Add(s, attack ? "training-lion-veteran" : Bower, a, Zone.Battlefield, province: 0);
                var claw = attack ? Add(s, tigerClaw, a, Zone.Attached, province: 0, parent: striker.Id) : striker;
                var tgt = targetUnit ? host : bystander;
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, attack ? Offer(e, a, TfGearAbilities.TigerClawAttack, claw.Id, tgt.Id) : Offer(e, a, Bow, striker.Id, tgt.Id));
                return e;
            }
            var e = Rig(true, true, true, out var host, out var spell, out var striker);
            Eq(e.CardForceForTest(host.Id), 1 + 4, "this Personality has +4F");
            var command = InWindow(e, "attackReaction", r, ability, spell.Id);
            Check(!e.State.Cards[striker.Id].Bowed, "nothing has bowed yet");
            Accept(e, command);
            Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as its cost");
            Settle(e);
            Check(e.State.Cards[striker.Id].Bowed, $"seat {r}: the action's performer bowed after it resolved");
            Check(e.State.Cards[host.Id].Zone == Zone.Battlefield, "the Attack (3) did not destroy the Shugenja (Force 5)");
            var control = Rig(true, true, true, out _, out var sp2, out var s2); Settle(control);
            Check(!control.State.Cards[s2.Id].Bowed && !control.State.Cards[sp2.Id].Bowed, "declined: neither the performer nor the Spell is bowed (the Tiger Claw's own cost bows the Claw)");
            var bowAction = Rig(true, false, true, out var h3, out var sp3, out var p3);
            var bowCommand = InWindow(bowAction, "beforeActionReaction", r, ability, sp3.Id);
            Accept(bowAction, bowCommand); Settle(bowAction);
            Check(bowAction.State.Cards[h3.Id].Bowed && bowAction.State.Cards[p3.Id].Bowed, "a bow action also bows its performer afterwards");
            var outside = Rig(true, true, false, out _, out var sp4, out _);
            Check(!CanUse(outside, r, ability, sp4.Id), "an action that targets a card outside the unit is not answered");
            var quiet = Rig(false, true, true, out _, out var sp5, out _);
            NeverOpened(quiet, r, ability, sp5.Id, command with { CardId = sp5.Id });
        }
    }

    private static void VerdantJungle()
    {
        const string ability = "w737-jungle-interrupt";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // The mover's controller `mover` is the Attacker when attackerMoves. The Region is at province `regionProvince` of player r.
            MatchEngine Rig(bool act, int regionProvince, bool attackerMoves, out CardInstance region, out CardInstance mover)
            {
                var s = Table(Phase.Battle, attackerMoves ? a : r); s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0, 2];
                var source = Add(s, "training-move-source", a, Zone.Battlefield, province: 0);
                mover = Add(s, Bower, a, Zone.Home);
                Add(s, Crane, r, Zone.Battlefield, province: 0);
                region = Add(s, def, r, Zone.Region, province: regionProvince);
                region.ProvinceIndex = regionProvince;
                s.PriorityPlayerId = a;
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Offer(e, a, "training-move-current", source.Id, mover.Id));
                return e;
            }
            var e = Rig(true, 0, true, out var region, out var mover);
            var command = InWindow(e, "beforeActionReaction", r, ability, region.Id);
            Accept(e, command); Settle(e);
            Check(e.State.Cards[region.Id].Bowed, $"seat {r}: the Region bowed as its cost");
            Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the movement into the attacking army was negated: he stays home");
            var control = Rig(true, 0, true, out _, out var m2); Settle(control);
            Eq(control.State.Cards[m2.Id].Zone, Zone.Battlefield, "declined, he moves to the battlefield");
            var elsewhere = Rig(true, 2, true, out var r3, out var m3);
            Check(!CanUse(elsewhere, r, ability, r3.Id), "another Province's Region opens nothing");
            Eq(elsewhere.State.Cards[m3.Id].Zone, Zone.Battlefield, "and the move happened");
            var defender = Rig(true, 0, false, out var r4, out _);
            Check(!CanUse(defender, r, ability, r4.Id), "a movement into the defending army is not an attacking army");
            var quiet = Rig(false, 0, true, out var r5, out _);
            NeverOpened(quiet, r, ability, r5.Id, command with { CardId = r5.Id });
        }
    }

    // ---- the synthetic training fixtures --------------------------------------------------------------------------------------------------------------------------------------
    private static void TrainingActionNegator()
    {
        const string ability = "training-negate-action";
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool own, bool targetMine, out CardInstance negator, out CardInstance target)
            {
                var s = Table(Phase.Battle, own ? r : a);
                negator = Add(s, "training-action-negator", r, Zone.Home);
                target = Add(s, Crane, r, Zone.Battlefield, province: 0);
                var foe = Add(s, Lion, a, Zone.Battlefield, province: 0);
                var bower = Add(s, Bower, own ? r : a, Zone.Battlefield, province: 0);
                var tgt = own ? foe : targetMine ? target : foe;
                if (own) tgt = foe;
                return act ? Open(s, own ? r : a, Bow, bower.Id, tgt.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, false, true, out var negator, out var target);
            var command = InWindow(e, "beforeActionReaction", r, ability, negator.Id);
            Accept(e, command); Settle(e);
            Check(!e.State.Cards[target.Id].Bowed, $"seat {r}: the action's effects were negated");
            var control = Rig(true, false, true, out _, out var t2); Settle(control);
            Check(control.State.Cards[t2.Id].Bowed, "declined, the bow lands");
            var s2 = Table(Phase.Action, a); s2.PriorityPlayerId = a; s2.Players[0].TurnsTaken = 3; s2.Players[1].TurnsTaken = 3;
            var idle = Add(s2, "training-action-negator", r, Zone.Home); var draw = Add(s2, "training-bushido-virtue", a, Zone.Hand);
            var untargeted = MatchEngine.FromTestState(s2);
            Accept(untargeted, Enabled(untargeted, a, "training-limited-draw", draw.Id).Command!);
            Check(untargeted.State.PendingChoice is null && !CanUse(untargeted, r, ability, idle.Id), "an action that targets no card of yours does not open it");
            var own = Rig(true, true, true, out var n4, out _);
            Check(!CanUse(own, r, ability, n4.Id), "your own action is not 'another player's'");
            var quiet = Rig(false, false, true, out var n5, out _);
            NeverOpened(quiet, r, ability, n5.Id, command with { CardId = n5.Id });
        }
    }

    private static void TrainingMoverNegator()
    {
        const string ability = "training-negate-mover";
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, out CardInstance negator, out CardInstance mover)
            {
                var s = Table(Phase.Battle, a); s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0, 2];
                var source = Add(s, "training-move-source", a, Zone.Battlefield, province: 0);
                mover = Add(s, Bower, a, Zone.Home);
                Add(s, Crane, r, Zone.Battlefield, province: 0);
                negator = Add(s, "training-mover-negator", r, Zone.Home);
                var e = MatchEngine.FromTestState(s);
                if (act) Accept(e, Offer(e, a, "training-move-current", source.Id, mover.Id));
                return e;
            }
            var e = Rig(true, out var negator, out var mover);
            var command = InWindow(e, "beforeActionReaction", r, ability, negator.Id, null, mover.Id);
            Accept(e, command); Settle(e);
            Eq(e.State.Cards[mover.Id].Zone, Zone.Home, $"seat {r}: his movement from the action was negated");
            var control = Rig(true, out _, out var m2); Settle(control);
            Eq(control.State.Cards[m2.Id].Zone, Zone.Battlefield, "declined, he moves");
            var quiet = Rig(false, out var n3, out var m3);
            NeverOpened(quiet, r, ability, n3.Id, command with { CardId = n3.Id, TargetId = m3.Id });
        }
    }

    private static void TrainingHonorGainNegator()
    {
        const string ability = "training-negate-honor-gain";
        const string charity = "oracle-l5r-10181";
        // The economy table: player 1 the active player, player 0 plays Virtuous Charity out of turn; player 1 holds the reactor.
        (MatchEngine E, CardInstance Card, CardInstance Reactor, string[] Sources, bool Reacted) Play(bool react, bool reactorHere = true)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
            var card = Add(s, charity, 0, Zone.Hand);
            var reactor = Add(s, "training-honor-gain-negator", reactorHere ? 1 : 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, "virtuous-charity-open", card.Id);
            var sources = option.Gold!.PaymentsByAmount![5][0].SourceIds;
            Accept(e, option.Command! with { GoldAmount = 5, SourceIds = sources });
            return (e, card, reactor, sources, react);
        }
        var (e, card, reactor, sources, _) = Play(true);
        foreach (var id in sources) Check(!e.State.Cards[id].Bowed, "nothing is paid while the window is open");
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window is open");
        var command = InWindow(e, "beforeActionReaction", 1, ability, reactor.Id);
        var honor0 = Honor(e, 0); var honor1 = Honor(e, 1);
        Accept(e, command); Settle(e, "beforeActionReaction");
        foreach (var id in sources) Check(e.State.Cards[id].Bowed, "the acting player's payment stands");
        Eq(e.State.PendingChoice?.Type, "opponentPayment", "the second player's decision opens");
        var decide = e.QueryActions(1).Single(x => x.Type == "chooseOpponentPayment");
        Accept(e, decide.Command! with { GoldAmount = 2, SourceIds = decide.Gold!.PaymentsByAmount![2][0].SourceIds });
        Check(e.State.Events.Count(x => x.Type == "payment") == 2, "both payments were made");
        Check(e.State.Events.Any(x => x.Type == "honorGainNegated"), "the Honor gain was negated");
        Eq(Honor(e, 0), honor0, "no Honor for the player who paid more"); Eq(Honor(e, 1), honor1, "and none for the other");
        // Control: without the reaction the difference (5 - 2 = 3) is gained.
        var (c, _, _, _, _) = Play(false);
        Settle(c, "beforeActionReaction");
        var cd = c.QueryActions(1).Single(x => x.Type == "chooseOpponentPayment");
        var before = Honor(c, 0);
        Accept(c, cd.Command! with { GoldAmount = 2, SourceIds = cd.Gold!.PaymentsByAmount![2][0].SourceIds });
        Eq(Honor(c, 0), before + 3, "declined, the one who paid more gains the 3 difference");
        // Held by the paying player himself, it is not 'another player's' action: not offered to him.
        var (own, _, ownReactor, _, _) = Play(true, false);
        Check(!CanUse(own, 0, ability, ownReactor.Id), "the acting player's own action is not another player's");
        // No window at all when nothing is being played.
        var s2 = Table(Phase.Action, 1); s2.PriorityPlayerId = 1; var idle = Add(s2, "training-honor-gain-negator", 1, Zone.Home);
        NeverOpened(MatchEngine.FromTestState(s2), 1, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void TrainingWeaponBreaker()
    {
        const string ability = "training-break-weapon";
        const string yumaru = "oracle-l5r-5181";
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // Player a's Yumaru (with a Weapon) targets an enemy Personality; player r holds the breaker.
            MatchEngine Rig(bool act, bool weapon, out CardInstance breaker, out CardInstance weaponCard, out CardInstance enemy, out CardInstance y)
            {
                var s = Table(Phase.Battle, a);
                y = Add(s, yumaru, a, Zone.Battlefield, province: 0);
                weaponCard = weapon ? Add(s, "training-weapon", a, Zone.Attached, province: 0, parent: y.Id) : null!;
                enemy = Add(s, Lion, r, Zone.Battlefield, province: 0);
                breaker = Add(s, "training-weapon-breaker", r, Zone.Battlefield, province: 0);
                return act ? Open(s, a, "yumaru-move-home", y.Id, enemy.Id) : MatchEngine.FromTestState(s);
            }
            var e = Rig(true, true, out var breaker, out var weaponCard, out var enemy, out var yum);
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action is held behind the reaction window");
            var command = InWindow(e, "beforeActionReaction", r, ability, breaker.Id);
            Eq(e.State.Cards[weaponCard.Id].Zone, Zone.Attached, "the Weapon is still attached before the Reaction");
            Accept(e, command);
            Eq(e.State.Cards[weaponCard.Id].Zone, Zone.FateDiscard, $"seat {r}: the Weapon attached to the action's source was destroyed");
            Settle(e, "beforeActionReaction");
            Check(e.State.PendingEffectChoice is null, "no Weapon any more: no choice is asked");
            Check(e.State.Cards[yum.Id].Zone == Zone.Home && e.State.Cards[enemy.Id].Zone == Zone.Home, "both went home (the action resolved without a Weapon)");
            var control = Rig(true, true, out _, out var w2, out _, out _); Settle(control, "beforeActionReaction");
            Eq(control.State.PendingChoice?.Type, "effectChoice", "declined, she still has the Weapon so the choice opens");
            Eq(control.State.Cards[w2.Id].Zone, Zone.Attached, "and the Weapon is untouched");
            var unarmed = Rig(true, false, out var b3, out _, out _, out _);
            Check(unarmed.State.PendingChoice is null || !CanUse(unarmed, r, ability, b3.Id), "with no Weapon on the source the Reaction has nothing to destroy");
            var quiet = Rig(false, true, out var b4, out _, out _, out _);
            NeverOpened(quiet, r, ability, b4.Id, command with { CardId = b4.Id });
        }
    }

    // ---- entering-play windows ----------------------------------------------------------------------------------------------------------------------------------------------
    private static GameCommand Paid(MatchEngine e, GameCommand command, int price) => command with { SourceIds = e.PaymentOptions(command.PlayerId, price).First().SourceIds };
    private static int Tokens(MatchEngine e, string id, string type) => e.State.Cards[id].Tokens.Count(t => t.Type == type);
    private static string[] NewCards(HashSet<string> before, MatchEngine e) => e.State.Cards.Keys.Where(k => !before.Contains(k)).ToArray();

    // Created cards: every new card satisfies the printed stats and keywords, and sits where the text puts it.
    private static void CheckCreated(MatchEngine e, string[] ids, int count, CardType type, int force, int? chi, int? honor, string[] keywords, string what)
    {
        Eq(ids.Length, count, what + ": number of cards created");
        foreach (var id in ids)
        {
            var d = CardCatalog.Get(e.State.Cards[id].DefinitionId);
            Check(d.Type == type && d.Force == force && (chi is null || d.Chi == chi) && (honor is null || d.PersonalHonor == honor), $"{what}: {d.Name} is {d.Force}F/{d.Chi}C/{d.PersonalHonor}PH {d.Type}");
            foreach (var k in keywords) Check((d.Keywords ?? []).Contains(k), $"{what}: {d.Name} has the keyword {k} (has {string.Join(",", d.Keywords ?? [])})");
        }
    }

    private static void AgashaRyo()
    {
        const string ability = "ryo-refill";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var refillId = e.State.Players[0].Provinces[0].CardId!;
        Check(refillId != id && !e.State.Cards[refillId].FaceUp, "the ordinary refill is face down");
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Eq(e.State.Players[0].Provinces[0].CardId, refillId, "the same refilled card");
        Check(e.State.Cards[refillId].FaceUp && e.State.Cards[refillId].Zone == Zone.Province, "the Province he entered from is refilled face up");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        // Declined: the refill stays face down.
        var (d, _) = Buy(def); var dr = d.State.Players[0].Provinces[0].CardId!;
        Settle(d);
        Check(!d.State.Cards[dr].FaceUp, "declined, the refill stays face down");
        var h = AtHome(def, out var ryo);
        NeverOpened(h, 0, ability, ryo.Id, command with { CardId = ryo.Id });
    }

    private static void AshigaruRecruits()
    {
        const string ability = "ashigaru-recruits-create";
        var def = Def(ability);
        var fledgling = CreatedCards.Resolve(AbilityCatalog.All[ability].Creates!).Id;
        MatchEngine Rig(out CardInstance recruits, out CardInstance wielder, out CardInstance second, bool equip = true)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            Gold(s, 0, 4);
            s.Players[0].Honor = 20; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
            wielder = Add(s, Crane, 0, Zone.Home); second = Add(s, "training-crane-veteran", 0, Zone.Home);
            recruits = Add(s, def, 0, equip ? Zone.Hand : Zone.Attached, parent: equip ? null : wielder.Id);
            var e = MatchEngine.FromTestState(s);
            if (equip) Accept(e, new GameCommand("equip", 0, e.Version, CardId: recruits.Id, SourceIds: e.PaymentOptions(0, CardCatalog.Get(def).GoldCost).FirstOrDefault()?.SourceIds ?? [], TargetId: wielder.Id));
            return e;
        }
        var e = Rig(out var recruits, out var wielder, out var second);
        Eq(e.State.PendingChoice?.Type, "entryReaction", "its entry window is open");
        var command = InWindow(e, "entryReaction", 0, ability, recruits.Id, null, second.Id);
        var offered = Options(e, 0, ability, recruits.Id).Single(o => o.Enabled).TargetIds!;
        Check(offered.Contains(wielder.Id) && offered.Contains(second.Id), "any of your Personalities is a target");
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var created = NewCards(before, e);
        CheckCreated(e, created, 1, CardType.Follower, 0, null, null, ["Ashigaru"], "Ashigaru Recruits");
        Check(e.State.Cards[created[0]].DefinitionId == fledgling && e.State.Cards[created[0]].Zone == Zone.Attached && e.State.Cards[created[0]].AttachedTo == second.Id, "the created 0F Ashigaru Follower is attached to the targeted Personality");
        var control = Rig(out _, out var w2, out _); Settle(control);
        Check(!control.State.Cards.Values.Any(c => c.DefinitionId == fledgling), "declined, nothing is created");
        var quiet = Rig(out var r3, out _, out var s3, false);
        NeverOpened(quiet, 0, ability, r3.Id, command with { CardId = r3.Id, TargetId = s3.Id });
        Check(w2.Id.Length > 0);
    }

    private static void DaigotsuKikumura()
    {
        const string ability = "w732-kikumura-invest";
        var def = Def(ability);
        foreach (var (clan, price) in new[] { ("Crane", 4), ("Dragon", 2) })
        {
            var (e, id) = Buy(def, null, 14, clan);
            var command = InWindow(e, "entryReaction", 0, ability, id);
            Eq(e.QuoteAbilityGold(command)?.Cost, price, $"a {clan} Clan player pays {price}");
            Reject(e, command with { SourceIds = [] });
            var before = e.State.Cards.Keys.ToHashSet();
            Accept(e, Paid(e, command, price));
            var followers = NewCards(before, e);
            CheckCreated(e, followers, 2, CardType.Follower, 1, null, null, [], $"{clan}: Kikumura's Invest");
            Check(followers.All(f => e.State.Cards[f].Zone == Zone.Attached && e.State.Cards[f].AttachedTo == id), "both are attached to Kikumura");
            Check(!CanUse(e, 0, ability, id), "once");
        }
        var (d, did) = Buy(def); var n = d.State.Cards.Count;
        Settle(d);
        Eq(d.State.Cards.Count, n, "declining creates nothing");
        var h = AtHome(def, out var k);
        NeverOpened(h, 0, ability, k.Id, new GameCommand("useAbility", 0, 0, CardId: k.Id, AbilityId: ability));
        Check(did.Length > 0);
    }

    private static void HirumaKoru()
    {
        const string ability = "proxy677-koru-invest";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Eq(e.QuoteAbilityGold(command)?.Cost, 1, "Invest 1");
        Reject(e, command with { SourceIds = [] });
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, Paid(e, command, 1));
        var made = NewCards(before, e);
        CheckCreated(e, made, 1, CardType.Follower, 0, null, null, [], "Koru's Invest");
        Check(e.State.Cards[made[0]].Zone == Zone.Attached && e.State.Cards[made[0]].AttachedTo == id, "the 0F Follower is attached to Koru");
        Check(!CanUse(e, 0, ability, id), "once");
        var (d, _) = Buy(def); var n = d.State.Cards.Count; Settle(d);
        Eq(d.State.Cards.Count, n, "declined, nothing is created");
        var h = AtHome(def, out var koru);
        NeverOpened(h, 0, ability, koru.Id, command with { CardId = koru.Id });
    }

    private static void IkomaJeiku()
    {
        const string ability = "w739-jeiku-invest";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var refill = e.State.Players[0].Provinces[0].CardId!;
        Check(!e.State.Cards[refill].FaceUp, "the Province refilled face down");
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Eq(e.QuoteAbilityGold(command)?.Cost, 1, "Invest 1");
        Reject(e, command with { SourceIds = [] });
        Check(!e.State.Cards[refill].FaceUp, "unpaid, nothing happened");
        Accept(e, Paid(e, command, 1));
        Eq(e.State.Players[0].Provinces[0].CardId, refill, "the same card");
        Check(e.State.Cards[refill].FaceUp, "turned face up");
        Check(!CanUse(e, 0, ability, id), "once");
        var (d, _) = Buy(def); var dr = d.State.Players[0].Provinces[0].CardId!; Settle(d);
        Check(!d.State.Cards[dr].FaceUp, "declined, still face down");
        var h = AtHome(def, out var jeiku);
        NeverOpened(h, 0, ability, jeiku.Id, command with { CardId = jeiku.Id });
    }

    private static void InvestTokens(string ability, int price, bool entryHonorLoss)
    {
        var def = Def(ability);
        var (e, id) = Buy(def);
        var honor = Honor(e, 0);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Eq(e.QuoteAbilityGold(command)?.Cost, price, $"Invest {price}");
        Reject(e, command with { SourceIds = [] });
        Eq(Tokens(e, id, "Fire"), 0, "unpaid, no tokens");
        var force = e.CardForceForTest(id);
        Accept(e, Paid(e, command, price));
        Eq(Tokens(e, id, "Fire"), 2, "two Fire tokens");
        Check(e.State.Cards[id].Tokens.All(t => t.Stat == "Force" && t.Amount == 1), "each is +1F");
        Eq(e.CardForceForTest(id), force + 2, "+2F in all");
        Check(!CanUse(e, 0, ability, id), "once");
        if (entryHonorLoss) Eq(Honor(e, 0), honor - 1, "After Hajime enters play, lose 1 Honor (after the Invest)");
        var (d, did) = Buy(def); Settle(d);
        Eq(Tokens(d, did, "Fire"), 0, "declined, no tokens");
        var h = AtHome(def, out var card);
        NeverOpened(h, 0, ability, card.Id, command with { CardId = card.Id });
    }

    private static void IsawaHajime() => InvestTokens("wl1-hajime-invest", 3, true);
    private static void TogashiNoritada() => InvestTokens("noritada-invest", 3, false);

    private static void KitsuSorano()
    {
        const string ability = "sorano-create";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var made = NewCards(before, e);
        CheckCreated(e, made, 2, CardType.Personality, 2, 2, 3, ["Lion Clan", "Samurai", "Ancestor", "Spirit"], "Sorano");
        Check(made.All(m => e.State.Cards[m].OwnerId == 0 && e.State.Cards[m].Zone == Zone.Home), "both are in play at home under her controller");
        var (d, _) = Buy(def); var n = d.State.Cards.Count; Settle(d);
        Eq(d.State.Cards.Count, n, "declined, nothing is created");
        var h = AtHome(def, out var sorano);
        NeverOpened(h, 0, ability, sorano.Id, command with { CardId = sorano.Id });
        // "or overlays": overlaying her onto the base printing opens the same entering-play window, with the same effect (either seat).
        foreach (var r in new[] { 0, 1 })
        {
            var s = Table(Phase.Dynasty, r); s.PriorityPlayerId = r;
            var basePrinting = Add(s, "oracle-l5r-4395", r, Zone.Home);
            var exp = s.Cards[s.Players[r].Provinces[0].CardId!];
            exp.DefinitionId = def; exp.FaceUp = true;
            var e2 = MatchEngine.FromTestState(s);
            Accept(e2, e2.QueryActions(r).First(o => o.Type == "overlay" && o.CardId == exp.Id && o.Enabled).Command!);
            Eq(e2.State.Cards[basePrinting.Id].Zone, Zone.RemovedFromGame, "the base printing left the game");
            var cmd = InWindow(e2, "entryReaction", r, ability, exp.Id);
            var pre = e2.State.Cards.Keys.ToHashSet();
            Accept(e2, cmd);
            var two = NewCards(pre, e2);
            CheckCreated(e2, two, 2, CardType.Personality, 2, 2, 3, ["Lion Clan", "Samurai", "Ancestor", "Spirit"], $"seat {r} overlay");
            Check(two.All(m => e2.State.Cards[m].OwnerId == r && e2.State.Cards[m].Zone == Zone.Home), "both are in play at home under her controller");
        }
    }

    private static void KitsuneHaruki()
    {
        const string ability = "haruki-create";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var made = NewCards(before, e);
        CheckCreated(e, made, 1, CardType.Personality, 2, 2, 3, ["Mantis Clan", "Nonhuman", "Deer", "Spirit"], "Haruki");
        var createdDef = e.State.Cards[made[0]].DefinitionId;
        var limited = (CardCatalog.Get(createdDef).Abilities ?? []).Single();
        Eq(AbilityCatalog.All[limited].Timing, "Limited", "the created Personality has the Limited ability");
        // "If another player has higher Family Honor than you: Gain 2 Honor."
        foreach (var (mine, theirs, offered) in new[] { (10, 20, true), (20, 20, false), (20, 10, false) })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
            var c = Add(s, createdDef, 0, Zone.Home); s.Players[0].Honor = mine; s.Players[1].Honor = theirs;
            var h = MatchEngine.FromTestState(s);
            Eq(CanUse(h, 0, limited, c.Id), offered, $"Family Honor {mine} vs {theirs}");
            if (offered) { Accept(h, Enabled(h, 0, limited, c.Id).Command!); Eq(Honor(h, 0), mine + 2, "gains 2 Honor"); }
        }
        var (d, _) = Buy(def); var n = d.State.Cards.Count; Settle(d);
        Eq(d.State.Cards.Count, n, "declined, nothing is created");
        var home = AtHome(def, out var haruki);
        NeverOpened(home, 0, ability, haruki.Id, command with { CardId = haruki.Id });
    }

    private static void KitsuneYamazaru()
    {
        const string ability = "proxy677-yamazaru-create";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var made = NewCards(before, e);
        CheckCreated(e, made, 1, CardType.Personality, 3, 2, 3, ["Mantis Clan", "Monkey", "Nonhuman", "Spirit"], "Yamazaru");
        var createdDef = e.State.Cards[made[0]].DefinitionId;
        var battle = (CardCatalog.Get(createdDef).Abilities ?? []).Single();
        Eq(AbilityCatalog.All[battle].Timing, "Battle", "the created Personality has a Battle ability");
        Check(e.State.Cards[made[0]].Zone == Zone.Home && e.State.Cards[made[0]].OwnerId == 0, "in play at home under his controller");
        var (d, _) = Buy(def); var n = d.State.Cards.Count; Settle(d);
        Eq(d.State.Cards.Count, n, "declined, nothing is created");
        var home = AtHome(def, out var yamazaru);
        NeverOpened(home, 0, ability, yamazaru.Id, command with { CardId = yamazaru.Id });
    }

    private static void SinghRemnants()
    {
        const string ability = "singh-remnants-attach";
        var def = Def(ability);
        var price = CardCatalog.Get(def).GoldCost;
        foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard })
        {
            CardInstance remnants = null!;
            var (e, id) = Buy("training-crane-clan-samurai", s => { remnants = Add(s, def, 0, zone); Gold(s, 0, 4); }, 14, "Crane");
            var command = InWindow(e, "entryReaction", 0, ability, remnants.Id);
            var honor = Honor(e, 0);
            Reject(e, command with { SourceIds = [] });
            Accept(e, Paid(e, command, price));
            Eq(e.State.Cards[remnants.Id].Zone, Zone.Attached, $"from the {zone}: attached");
            Eq(e.State.Cards[remnants.Id].AttachedTo, id, "to the Personality that entered");
            Eq(Honor(e, 0), honor + 1, "gains 1 Honor");
        }
        CardInstance r2 = null!;
        var (d, _) = Buy("training-crane-clan-samurai", s => r2 = Add(s, def, 0, Zone.Hand), 14, "Crane");
        Settle(d);
        Eq(d.State.Cards[r2.Id].Zone, Zone.Hand, "declined, it stays in hand");
        // Neither in the hand nor the discard pile (here: the Fate deck) it is not offered.
        CardInstance r3 = null!;
        var (f, _) = Buy("training-crane-clan-samurai", s => r3 = Add(s, def, 0, Zone.FateDeck), 14, "Crane");
        Check(!CanUse(f, 0, ability, r3.Id), "a card in the deck is not offered");
        var s0 = Table(Phase.Action, 0); s0.PriorityPlayerId = 0;
        var idle = Add(s0, def, 0, Zone.Hand);
        var quiet = MatchEngine.FromTestState(s0);
        NeverOpened(quiet, 0, ability, idle.Id, new GameCommand("useAbility", 0, 0, CardId: idle.Id, AbilityId: ability));
    }

    // ---- engage windows -----------------------------------------------------------------------------------------------------------------------------------------------------
    // The Attack Phase's battle selection with `attacker` choosing province 0: each player has a unit there. Engages when asked.
    private static MatchEngine Engaging(int attacker, bool engage, Action<MatchState>? put = null, bool defenderUnit = true, bool expectWindow = true)
    {
        var s = Table(Phase.BattleSelection, attacker); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, Crane, attacker, Zone.Battlefield, province: 0);
        if (defenderUnit) Add(s, Lion, 1 - attacker, Zone.Battlefield, province: 0);
        put?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        if (engage) { Accept(e, new GameCommand("selectBattle", attacker, e.Version, Province: 0)); if (expectWindow) Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens"); }
        return e;
    }

    private static void IuchiTakaki()
    {
        const string ability = "wl-pc-takaki-madness";
        var def = Def(ability);
        foreach (var attacker in new[] { 0, 1 })
            foreach (var r in new[] { 0, 1 })
            {
                var a = 1 - r;
                MatchEngine Rig(bool engage, out CardInstance takaki, out CardInstance friend, out CardInstance foe)
                {
                    CardInstance t = null!, f = null!, o = null!;
                    var e = Engaging(attacker, engage, s => { t = Add(s, def, r, Zone.Battlefield, province: 0); f = Add(s, "training-crane-veteran", r, Zone.Battlefield, province: 0); o = Add(s, "training-lion-veteran", a, Zone.Battlefield, province: 0); });
                    takaki = t; friend = f; foe = o; return e;
                }
                var e = Rig(true, out var takaki, out var friend, out var foe);
                var command = InWindow(e, "engageReaction", r, ability, takaki.Id, null, foe.Id);
                var targets = Options(e, r, ability, takaki.Id).SelectMany(o => o.TargetIds ?? []).ToArray();
                Check(targets.Contains(foe.Id) && targets.Contains(friend.Id) && targets.Contains(takaki.Id), "any card in play is a target");
                Accept(e, command); Settle(e, "engageReaction");
                Eq(Tokens(e, foe.Id, "Madness"), 1, $"attacker {attacker}, reactor {r}: the target has a Madness token");
                Eq(Tokens(e, friend.Id, "Madness"), 0, "no other card does");
                Check(!CanUse(e, r, ability, takaki.Id), "not offered once the engagement is over");
                var quiet = Rig(false, out var t2, out _, out _);
                NeverOpened(quiet, r, ability, t2.Id, command with { CardId = t2.Id });
            }
    }

    private static void LostEquipment()
    {
        const string ability = "wlsac-lost-equipment";
        var def = Def(ability);
        foreach (var attacker in new[] { 0, 1 })
            foreach (var r in new[] { 0, 1 })
            {
                var a = 1 - r;
                MatchEngine Rig(bool engage, out CardInstance card, out CardInstance enemyWeapon, out CardInstance ownWeapon, out CardInstance awayWeapon)
                {
                    CardInstance c = null!, ew = null!, ow = null!, aw = null!;
                    var e = Engaging(attacker, engage, s =>
                    {
                        var foe = Add(s, "training-lion-veteran", a, Zone.Battlefield, province: 0); ew = Add(s, "training-weapon", a, Zone.Attached, province: 0, parent: foe.Id);
                        var mine = Add(s, "training-crane-veteran", r, Zone.Battlefield, province: 0); ow = Add(s, "training-weapon", r, Zone.Attached, province: 0, parent: mine.Id);
                        var away = Add(s, Lion, a, Zone.Home); aw = Add(s, "training-weapon", a, Zone.Attached, parent: away.Id);
                        c = Add(s, def, r, Zone.Hand);
                    });
                    card = c; enemyWeapon = ew; ownWeapon = ow; awayWeapon = aw; return e;
                }
                var e = Rig(true, out var card, out var weapon, out var own, out var away);
                var command = InWindow(e, "engageReaction", r, ability, card.Id, null, weapon.Id);
                var targets = Options(e, r, ability, card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
                Eq(string.Join(",", targets), weapon.Id, "only the enemy attachment at the battlefield is a target");
                Reject(e, command with { TargetId = own.Id }); Reject(e, command with { TargetId = away.Id });
                Accept(e, command);
                Check(e.State.Cards[weapon.Id].Bowed, $"attacker {attacker}, reactor {r}: the enemy attachment is bowed");
                Check(!e.State.Cards[own.Id].Bowed && !e.State.Cards[away.Id].Bowed, "no other attachment is");
                Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
                var quiet = Rig(false, out var c2, out _, out _, out _);
                NeverOpened(quiet, r, ability, c2.Id, command with { CardId = c2.Id });
            }
    }

    private static void WinterSled()
    {
        const string ability = "pf-sled-move";
        var def = Def(ability);
        foreach (var attacker in new[] { 0, 1 })
            foreach (var r in new[] { 0, 1 })
            {
                var a = 1 - r;
                MatchEngine Rig(bool engage, bool enemyThere, bool bowedSled, out CardInstance sled, out CardInstance wearer)
                {
                    CardInstance sl = null!, w = null!;
                    var e = Engaging(attacker, engage, s =>
                    {
                        w = Add(s, "training-crane-veteran", r, Zone.Home); sl = Add(s, def, r, Zone.Attached, bowedSled, null, w.Id);
                        if (!enemyThere) foreach (var c in s.Cards.Values.Where(c => c.OwnerId == a && c.Zone == Zone.Battlefield).ToArray()) s.Cards.Remove(c.Id);
                    }, true, enemyThere && !bowedSled);
                    sled = sl; wearer = w; return e;
                }
                var e = Rig(true, true, false, out var sled, out var wearer);
                var command = InWindow(e, "engageReaction", r, ability, sled.Id);
                Accept(e, command); Settle(e, "engageReaction");
                Check(e.State.Cards[wearer.Id].Zone == Zone.Battlefield && e.State.Cards[wearer.Id].ProvinceIndex == 0, $"attacker {attacker}, reactor {r}: the Personality moved to the battlefield");
                Check(e.State.Cards[sled.Id].Bowed, "the Sled bowed as the cost");
                var alone = Rig(true, false, false, out var s2, out _);
                Check(!CanUse(alone, r, ability, s2.Id), "no enemy units at the battlefield: not offered");
                var bowed = Rig(true, true, true, out var s3, out _);
                Check(!CanUse(bowed, r, ability, s3.Id), "a bowed Sled cannot pay its cost");
                var quiet = Rig(false, true, false, out var s4, out _);
                NeverOpened(quiet, r, ability, s4.Id, command with { CardId = s4.Id });
            }
    }

    private static void UtakuJiYun()
    {
        const string ability = "w734-jiyun-lock";
        var def = Def(ability);
        foreach (var attacker in new[] { 0, 1 })
            foreach (var r in new[] { 0, 1 })
            {
                var a = 1 - r;
                MatchEngine Rig(bool engage, bool atBattle, bool react, out CardInstance jiyun, out CardInstance foe)
                {
                    CardInstance j = null!, f = null!;
                    var e = Engaging(attacker, engage, s =>
                    {
                        j = atBattle ? Add(s, def, r, Zone.Battlefield, province: 0) : Add(s, def, r, Zone.Home);
                        f = Add(s, Bower, a, Zone.Battlefield, province: 0);
                    }, true, atBattle);
                    jiyun = j; foe = f;
                    if (engage && react)
                    {
                        if (e.State.PriorityPlayerId != r) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
                        Accept(e, Offer(e, r, ability, j.Id, f.Id));
                        Settle(e, "engageReaction");
                    }
                    return e;
                }
                // The opposing Personality can use his Battle ability once the Engage window is over... unless Ji-Yun locked him.
                bool CanBow(MatchEngine e, CardInstance foe)
                {
                    if (e.State.PriorityPlayerId != foe.OwnerId && e.State.PendingChoice is null) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
                    return CanUse(e, foe.OwnerId, Bow, foe.Id);
                }
                var window = Rig(true, true, false, out var jiyun, out var foe);
                var command = InWindow(window, "engageReaction", r, ability, jiyun.Id, null, foe.Id);
                var targets = Options(window, r, ability, jiyun.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
                Check(targets.Contains(foe.Id) && !targets.Contains(jiyun.Id), "an opposing Personality is a target, not Ji-Yun herself");
                var locked = Rig(true, true, true, out _, out var f2);
                var control = Rig(true, true, false, out _, out var f3); Settle(control, "engageReaction");
                Check(CanBow(control, f3), $"attacker {attacker}, reactor {r}: control, the foe may use his ability");
                Check(!CanBow(locked, f2), "after the Reaction he may not use his abilities");
                Reject(locked, new GameCommand("useAbility", f2.OwnerId, locked.Version, CardId: f2.Id, AbilityId: Bow, TargetId: locked.State.Cards.Values.First(c => c.OwnerId == r && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Id));
                var home = Rig(true, false, false, out var j4, out _);
                Check(!CanUse(home, r, ability, j4.Id), "Ji-Yun at home is not engaging at her battlefield");
                var quiet = Rig(false, true, false, out var j5, out _);
                NeverOpened(quiet, r, ability, j5.Id, command with { CardId = j5.Id });
            }
    }

    private static void FruitlessSearch()
    {
        const string ability = "fruitless-search-create";
        var def = Def(ability);
        var magistrate = CreatedCards.Resolve(AbilityCatalog.All[ability].Creates!).Id;
        foreach (var attacker in new[] { 0, 1 })
            foreach (var r in new[] { 0, 1 })
            {
                var a = 1 - r;
                MatchEngine Rig(bool engage, bool enemy, out CardInstance card)
                {
                    CardInstance c = null!;
                    var e = Engaging(attacker, engage, s =>
                    {
                        c = Add(s, def, r, Zone.Hand);
                        if (!enemy) foreach (var u in s.Cards.Values.Where(u => u.OwnerId == a && u.Zone == Zone.Battlefield).ToArray()) s.Cards.Remove(u.Id);
                    }, true, enemy);
                    card = c; return e;
                }
                var e = Rig(true, true, out var card);
                var command = InWindow(e, "engageReaction", r, ability, card.Id);
                var before = e.State.Cards.Keys.ToHashSet();
                Accept(e, command); Settle(e, "engageReaction");
                var made = NewCards(before, e);
                CheckCreated(e, made, 1, CardType.Personality, 4, 4, 3, ["Imperial", "Samurai", "Duelist", "Magistrate"], "Fruitless Search");
                var created = e.State.Cards[made[0]];
                Check(created.DefinitionId == magistrate && created.OwnerId == r && created.Zone == Zone.Battlefield && created.ProvinceIndex == 0, $"attacker {attacker}, reactor {r}: on the reactor's side at the battlefield");
                Check(e.State.CreatedUntilBattleEndsIds.Contains(created.Id), "marked to be removed after this battle");
                RunUntil(e, () => e.State.Events.Any(x => x.Type == "battleResolved"));
                Eq(e.State.Cards[created.Id].Zone, Zone.RemovedFromGame, "removed from the game after the battle ends");
                var alone = Rig(true, false, out var c2);
                Check(!CanUse(alone, r, ability, c2.Id), "no enemy units at the battlefield: not offered");
                var quiet = Rig(false, true, out var c3);
                NeverOpened(quiet, r, ability, c3.Id, command with { CardId = c3.Id });
            }
    }

    // ---- after-action windows ------------------------------------------------------------------------------------------------------------------------------------------------
    // Declines windows until the named one opens (or none is left).
    // Passes for whoever must act until the named window opens (the battle phase only: a pass hands priority on, two end the battle).
    private static void ToWindow(MatchEngine e, string type, bool mayEnd = false)
    {
        for (var guard = 0; guard < 80 && e.State.PendingChoice?.Type != type; guard++)
        {
            if (mayEnd && e.State.PendingChoice is null && e.State.Events.Any(x => x.Type == "battleResolved")) return; // the battle ended with no such window
            var step = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }.SelectMany(p => e.QueryActions(p)).FirstOrDefault(x => x.Enabled && x.Command is not null && x.Type is "declineReaction" or "pass");
            Check(step is not null, $"nothing to pass with on the way to {type} ({e.State.PendingChoice?.Type})");
            Accept(e, step!.Command!);
        }
        Check(mayEnd || e.State.PendingChoice?.Type == type, $"never reached {type}");
    }

    private static void Drain(MatchEngine e, string type)
    {
        for (var guard = 0; guard < 40 && e.State.PendingChoice is not null && e.State.PendingChoice.Type != type; guard++)
        {
            var step = new[] { e.State.PriorityPlayerId, e.State.PendingChoice.PlayerId }.SelectMany(p => e.QueryActions(p)).FirstOrDefault(x => x.Enabled && x.Command is not null && x.Type is "declineReaction" or "pass");
            Check(step is not null, $"nothing to decline on the way to {type} ({e.State.PendingChoice?.Type})");
            Accept(e, step!.Command!);
        }
    }
    private static int ForceMods(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    private static void Erosion()
    {
        const string ability = "erosion-weaken";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool act, bool water, out CardInstance erosion, out CardInstance mover)
            {
                var s = Table(Phase.Battle, a);
                var shugenja = Kw(Add(s, Crane, r, Zone.Battlefield, province: 0), "Shugenja");
                if (water) Kw(shugenja, "Water");
                erosion = Add(s, def, r, Zone.Attached, province: 0, parent: shugenja.Id);
                Add(s, Lion, a, Zone.Battlefield, province: 0);
                mover = Add(s, Lion, a, Zone.Home);
                var strategy = Add(s, "training-move-battle-strategy", a, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act) { Accept(e, Offer(e, a, "training-move-to-battle", strategy.Id, mover.Id)); Drain(e, "afterActionResolvedReaction"); }
                return e;
            }
            var plain = Rig(false, true, out _, out _);
            Eq(plain.ProvinceStrengthForTest(a, 0), plain.State.Players[a].Provinces[0].Strength - 1, $"seat {r}: while the Shugenja is Water, other players' provinces have -1 strength");
            Eq(plain.ProvinceStrengthForTest(r, 0), plain.State.Players[r].Provinces[0].Strength, "its own player's are untouched");
            var dry = Rig(false, false, out _, out _);
            Eq(dry.ProvinceStrengthForTest(a, 0), dry.State.Players[a].Provinces[0].Strength, "not Water: no penalty");
            var e = Rig(true, true, out var erosion, out var mover);
            var command = InWindow(e, "afterActionResolvedReaction", r, ability, erosion.Id, null, mover.Id);
            var force = e.CardForceForTest(mover.Id);
            Accept(e, command);
            Eq(ForceMods(e, mover.Id), -4, "the moved Personality has -4F");
            Eq(e.CardForceForTest(mover.Id), force - 4, "and his current Force fell by 4");
            var quiet = Rig(false, true, out var e4, out var m4);
            NeverOpened(quiet, r, ability, e4.Id, command with { CardId = e4.Id, TargetId = m4.Id });
            // A move home moved no Personality to the current battlefield.
            var s2 = Table(Phase.Battle, a);
            var sh = Kw(Add(s2, Crane, r, Zone.Battlefield, province: 0), "Shugenja");
            var er2 = Add(s2, def, r, Zone.Attached, province: 0, parent: sh.Id);
            var courtier = Add(s2, "training-crane-courtier", r, Zone.Battlefield, province: 0);
            Add(s2, Lion, a, Zone.Battlefield, province: 0);
            var incap = Add(s2, "oracle-l5r-3730", a, Zone.Hand);
            var h = MatchEngine.FromTestState(s2);
            Accept(h, Offer(h, a, "incapacitated", incap.Id, courtier.Id));
            Drain(h, "afterActionResolvedReaction");
            Eq(h.State.Cards[courtier.Id].Zone, Zone.Home, "the defender moved home");
            Check(!CanUse(h, r, ability, er2.Id), "a move home offers Erosion nothing");
        }
    }

    private static void HidaDefenders()
    {
        const string ability = "hida-defenders-honor";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // Actor: whose Battle action bows the opposing Personality. `foeHome`: the opposing Personality stands at home instead.
            MatchEngine Rig(int actor, bool foeAtBattle, out CardInstance defenders, out CardInstance foe, bool act = true)
            {
                var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor;
                var host = Add(s, Crane, r, Zone.Battlefield, province: 0);
                defenders = Add(s, def, r, Zone.Attached, province: 0, parent: host.Id);
                foe = foeAtBattle ? Add(s, Lion, a, Zone.Battlefield, province: 0) : Add(s, Lion, a, Zone.Home);
                if (!foeAtBattle) Add(s, Lion, a, Zone.Battlefield, province: 0);
                var bower = Add(s, Bower, actor, Zone.Battlefield, province: 0);
                var e = MatchEngine.FromTestState(s);
                var target = actor == r ? foe : host;
                if (act) { Accept(e, Offer(e, actor, Bow, bower.Id, target.Id)); Drain(e, "afterActionResolvedReaction"); }
                return e;
            }
            var e = Rig(r, true, out var defenders, out var foe);
            var command = InWindow(e, "afterActionResolvedReaction", r, ability, defenders.Id);
            var honor = Honor(e, r); var other = Honor(e, a);
            Accept(e, command);
            Eq(Honor(e, r), honor + 2, $"seat {r}: an opposing Personality was bowed by your action: +2 Honor");
            Eq(Honor(e, a), other, "the other player's Honor is untouched");
            Check(e.State.Cards[foe.Id].Bowed, "the bow itself resolved");
            var theirs = Rig(a, true, out var d2, out _);
            Check(!CanUse(theirs, r, ability, d2.Id), "another player's action is not 'your action'");
            // An action of his that bowed, destroyed and moved nothing (a Force boost) gives nothing.
            var s2 = Table(Phase.Battle, r); s2.PriorityPlayerId = r;
            var host2 = Add(s2, Crane, r, Zone.Battlefield, province: 0); var def2 = Add(s2, def, r, Zone.Attached, province: 0, parent: host2.Id);
            Add(s2, Lion, a, Zone.Battlefield, province: 0);
            var boost = Add(s2, "training-performer-strategy", r, Zone.Hand);
            var plain = MatchEngine.FromTestState(s2);
            Accept(plain, Enabled(plain, r, "training-performer-boost", boost.Id, host2.Id).Command!);
            Drain(plain, "afterActionResolvedReaction");
            Check(!CanUse(plain, r, ability, def2.Id), "an action that bowed, destroyed or moved nobody gives nothing");
            var quiet = Rig(r, true, out var d4, out _, false);
            NeverOpened(quiet, r, ability, d4.Id, command with { CardId = d4.Id });
        }
    }

    private static void MatsuRika()
    {
        const string ability = "rika-bushido-virtue-bonus";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(int actor, bool virtue, int cards, out CardInstance rika, out CardInstance performer, out CardInstance[] strategies)
            {
                var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor;
                rika = Add(s, def, r, Zone.Battlefield, province: 0);
                performer = Add(s, Crane, actor, Zone.Battlefield, province: 0);
                if (actor != r) Add(s, Lion, r, Zone.Battlefield, province: 0);
                strategies = Enumerable.Range(0, cards).Select(_ => { var c = Add(s, "training-performer-strategy", actor, Zone.Hand); if (virtue) Kw(c, "Bushido Virtue"); return c; }).ToArray();
                return MatchEngine.FromTestState(s);
            }
            var e = Rig(r, true, 4, out var rika, out var performer, out var cards);
            var force = e.CardForceForTest(rika.Id);
            var used = 0; GameCommand? first = null;
            for (var i = 0; i < cards.Length; i++)
            {
                e = Batch336Support.Edit(e, st => { st.PriorityPlayerId = r; st.ConsecutivePasses = 0; });
                Accept(e, Enabled(e, r, "training-performer-boost", cards[i].Id, performer.Id).Command!);
                if (i < 3) Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "her own Bushido Virtue action opens the window");
                else { Check(!CanUse(e, r, ability, rika.Id), "the fourth time this turn she is not offered it"); continue; }
                var cmd = i == 0 ? InWindow(e, "afterActionResolvedReaction", r, ability, rika.Id) : Enabled(e, r, ability, rika.Id).Command!;
                first ??= cmd;
                Accept(e, cmd); used++;
                Settle(e, "afterActionResolvedReaction");
            }
            Eq(used, 3, $"seat {r}: three times per turn");
            Eq(e.CardForceForTest(rika.Id), force + 3, "+1F each time");
            // Not for a non-Bushido-Virtue action of her own controller, nor another player's Bushido Virtue action.
            var plain = Rig(r, false, 1, out var r2, out var p2, out var c2);
            Accept(plain, Enabled(plain, r, "training-performer-boost", c2[0].Id, p2.Id).Command!);
            Check(!CanUse(plain, r, ability, r2.Id), "the action carried no Bushido Virtue keyword");
            var foes = Rig(a, true, 1, out var r3, out var p3, out var c3);
            Accept(foes, Enabled(foes, a, "training-performer-boost", c3[0].Id, p3.Id).Command!);
            Check(!CanUse(foes, r, ability, r3.Id), "another player's Bushido Virtue action does not count");
            var quiet = Rig(r, true, 1, out var r4, out _, out _);
            NeverOpened(quiet, r, ability, r4.Id, first! with { CardId = r4.Id });
        }
    }

    private static void RataDraw()
    {
        const string ability = "rata-draw";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // Rata (or another Personality of the same player) attacks with a Melee 2 Weapon: the Force 2 foe dies, a Force 10 foe survives.
            MatchEngine Rig(bool viaRata, bool kills, out CardInstance rata, out CardInstance foe, bool act = true)
            {
                var s = Table(Phase.Battle, r);
                rata = Add(s, def, r, Zone.Battlefield, province: 0);
                var wielder = viaRata ? rata : Add(s, Crane, r, Zone.Battlefield, province: 0);
                var weapon = Add(s, "training-weapon-with-attack", r, Zone.Attached, province: 0, parent: wielder.Id);
                foe = Add(s, "training-recon-attacker", a, Zone.Battlefield, province: 0);
                if (!kills) Force(foe, 10);
                var e = MatchEngine.FromTestState(s);
                if (act) { Accept(e, Offer(e, r, "training-weapon-attack", weapon.Id, foe.Id)); }
                return e;
            }
            var e = Rig(true, true, out var rata, out var foe);
            Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "precondition: Rata's own action destroyed a Personality");
            var command = InWindow(e, "afterActionResolvedReaction", r, ability, rata.Id);
            var hand = e.State.Players[r].Hand.Count; var other = e.State.Players[a].Hand.Count;
            Accept(e, command);
            Eq(e.State.Players[r].Hand.Count, hand + 1, $"seat {r}: drew a card");
            Eq(e.State.Players[a].Hand.Count, other, "and the other player did not");
            var survives = Rig(true, false, out var r2, out _);
            Check(!CanUse(survives, r, ability, r2.Id), "the action destroyed no Personality");
            var notRata = Rig(false, true, out var r3, out var f3);
            Eq(notRata.State.Cards[f3.Id].Zone, Zone.Dead, "precondition: another Personality's action destroyed him");
            Check(!CanUse(notRata, r, ability, r3.Id), "an action Rata did not perform gives him nothing");
            var quiet = Rig(true, true, out var r4, out _, false);
            NeverOpened(quiet, r, ability, r4.Id, command with { CardId = r4.Id });
        }
    }

    private static void ShinjoJongHo()
    {
        const string ability = "jongho-bow";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Rig(bool jongHoPerforms, out CardInstance jongho, out CardInstance weak, out CardInstance strong, bool act = true)
            {
                var s = Table(Phase.Battle, r);
                foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-6855";
                jongho = Add(s, def, r, Zone.Battlefield, province: 0);
                var tactician = jongHoPerforms ? jongho : Kw(Add(s, "training-crane-veteran", r, Zone.Battlefield, province: 0), "Tactician");
                weak = Add(s, Crane, a, Zone.Battlefield, province: 0); Force(weak, -2);
                strong = Add(s, Lion, a, Zone.Battlefield, province: 0); Force(strong, 3);
                Add(s, Crane, r, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                if (act)
                {
                    var option = e.QueryActions(r).First(x => x.Type == "useAbility" && x.Enabled && x.Command?.AbilityId == "tactical-advantage" && x.Command.PerformerId == tactician.Id);
                    Accept(e, option.Command!);
                }
                return e;
            }
            var e = Rig(true, out var jongho, out var weak, out var strong);
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for Jong-Ho");
            var command = InWindow(e, "afterActionResolvedReaction", r, ability, jongho.Id, null, weak.Id);
            var targets = Options(e, r, ability, jongho.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
            Check(targets.Contains(weak.Id) && !targets.Contains(strong.Id), "only an opposing card with lower Force: " + string.Join(",", targets));
            Accept(e, command);
            Check(e.State.Cards[weak.Id].Bowed && !e.State.Cards[strong.Id].Bowed, $"seat {r}: the lower-Force card is bowed, the other is not");
            var other = Rig(false, out var j2, out _, out _);
            Check(!CanUse(other, r, ability, j2.Id), "a Tactical action he did not perform gives him nothing");
            var quiet = Rig(true, out var j3, out _, out _, false);
            NeverOpened(quiet, r, ability, j3.Id, command with { CardId = j3.Id });
        }
    }

    // ---- duel-won and after-resolution windows -----------------------------------------------------------------------------------------------------------------------
    private static void KakitaTadanobu()
    {
        const string ability = "tadanobu-draw";
        var def = Def(ability);
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            MatchEngine Duel(bool favor, bool wins, out CardInstance tadanobu)
            {
                var s = Table(Phase.Battle, r); s.PriorityPlayerId = r;
                tadanobu = Add(s, def, r, Zone.Battlefield, province: 0);
                var challenge = Add(s, "training-duel-challenge", r, Zone.Hand);
                var enemy = Add(s, Lion, a, Zone.Battlefield, province: 0);
                if (!wins) enemy.Modifiers.Add(new("test", "test", "Chi", 10));
                if (favor) s.FavorPlayerId = r;
                var e = MatchEngine.FromTestState(s);
                Accept(e, new GameCommand("useAbility", r, e.Version, CardId: challenge.Id, PerformerId: tadanobu.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
                Accept(e, new GameCommand("acceptDuel", a, e.Version));
                for (var guard = 0; e.State.PendingDuel is { } duel && guard < 10; guard++)
                    Accept(e, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", duel.TurnPlayerId, e.Version));
                return e;
            }
            var e = Duel(true, true, out var tadanobu);
            Eq(e.State.PendingChoice?.Type, "duelWonReaction", "the duel-won window opens for the winner");
            Eq(e.State.PendingChoice?.CardId, tadanobu.Id, "scoped to the Personality who won");
            var command = InWindow(e, "duelWonReaction", r, ability, tadanobu.Id);
            var hand = e.State.Players[r].Hand.Count; var other = e.State.Players[a].Hand.Count;
            Accept(e, command);
            Eq(e.State.Players[r].Hand.Count, hand + 2, $"seat {r}: two cards drawn");
            Eq(e.State.Players[a].Hand.Count, other, "the other player draws none");
            Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded as the cost");
            var noFavor = Duel(false, true, out var t2);
            Eq(noFavor.State.PendingChoice?.Type, "duelWonReaction");
            Check(!CanUse(noFavor, r, ability, t2.Id), "the Favor must be his controller's");
            Check(Rejection(noFavor, Use(noFavor, r, ability, t2.Id)).Contains("Imperial Favor"), "refused with the reason");
            var lost = Duel(true, false, out var t3);
            Check(lost.State.PendingChoice?.Type != "duelWonReaction" && !CanUse(lost, r, ability, t3.Id), "a duel he lost gives no window");
        }
    }

    private static void ReturningHome()
    {
        const string ability = "returning-home-create";
        var def = Def(ability);
        const string holding = "oracle-l5r-10024";
        foreach (var r in new[] { 0, 1 })
        {
            var a = 1 - r;
            // `attackerWins`: the Attacker `r` destroys the province; otherwise the Defender beats him. `asDefender`: the card's controller defends.
            MatchEngine Rig(bool attackerWins, bool asDefender, out CardInstance card)
            {
                var attacker = asDefender ? a : r;
                var s = Table(Phase.Battle, attacker);
                var strong = Add(s, "training-lion-veteran", attackerWins ? attacker : 1 - attacker, Zone.Battlefield, province: 0);
                Force(strong, 30);
                Add(s, Crane, attackerWins ? 1 - attacker : attacker, Zone.Battlefield, province: 0);
                card = Add(s, def, r, Zone.Hand);
                s.Players[r].FateDeck.Add(Add(s, Lion, r, Zone.FateDeck).Id);
                var e = MatchEngine.FromTestState(s);
                ToWindow(e, "afterResolutionReaction", !attackerWins || asDefender);
                return e;
            }
            var e = Rig(true, false, out var card);
            Eq(e.State.PendingAfterResolution!.ProvinceDestroyedBy, r, "precondition: the Attacker destroyed the province");
            var command = InWindow(e, "afterResolutionReaction", r, ability, card.Id);
            var holdings = e.State.Cards.Values.Count(c => c.OwnerId == r && c.Zone == Zone.Home && c.DefinitionId == holding);
            var hand = e.State.Players[r].Hand.Count;
            Accept(e, command);
            var created = e.State.Cards.Values.Where(c => c.OwnerId == r && c.Zone == Zone.Home && c.DefinitionId == holding).ToArray();
            Eq(created.Length, holdings + 1, $"seat {r}: a Holding was created at home");
            Check(created.All(h => h.Bowed), "it entered play bowed");
            Eq(CardCatalog.Get(holding).GoldProduction, 3, "and produces 3 Gold when it straightens");
            Eq(e.State.Players[r].Hand.Count, hand - 1 + 1, "Returning Home left the hand and a card was drawn");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "Returning Home was played");
            var lost = Rig(false, false, out var c2);
            Check(lost.State.PendingChoice is null || !CanUse(lost, r, ability, c2.Id), "the Attacker lost: nothing offered");
            var defending = Rig(true, true, out var c3);
            Check(defending.State.PendingChoice is null || !CanUse(defending, r, ability, c3.Id), "as the Defender (the other player destroyed the province) it is not offered");
            var s0 = Table(Phase.Battle, r); var idle = Add(s0, def, r, Zone.Hand);
            NeverOpened(MatchEngine.FromTestState(s0), r, ability, idle.Id, command with { CardId = idle.Id });
        }
    }

    // ---- payment reactions: "when paying a Gold cost" ---------------------------------------------------------------------------------------------------------------------
    private static string PayToken(CardInstance card, string mode) => card.Id + "#" + mode;
    private static void BowAllGold(MatchState s, int owner) { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true; }
    private static GameCommand Equip(MatchEngine e, int player, CardInstance item, CardInstance host, params string[] sources) => new("equip", player, e.Version, CardId: item.Id, SourceIds: sources, TargetId: host.Id);

    private static void SeasideBazaar()
    {
        const string ability = "seaside-bazaar-gold";
        var def = Def(ability);
        foreach (var p in new[] { 0, 1 })
        {
            var q = 1 - p;
            MatchEngine Rig(bool bowedBazaar, out CardInstance bazaar, out CardInstance host, out CardInstance item, out CardInstance item2)
            {
                var s = Table(Phase.Action, p); s.PriorityPlayerId = p; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
                BowAllGold(s, 0); BowAllGold(s, 1);
                bazaar = Add(s, def, p, Zone.Home, bowedBazaar);
                host = Add(s, Crane, p, Zone.Home);
                item = Add(s, "training-item", p, Zone.Hand); item2 = Add(s, "training-item", p, Zone.Hand);
                Add(s, def, q, Zone.Home); // the other player's own Bazaar, to prove that one never pays for ours
                return MatchEngine.FromTestState(s);
            }
            var e = Rig(false, out var bazaar, out var host, out var item, out var item2);
            var paying = new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip);
            var token = PayToken(bazaar, "reaction");
            Eq(e.GoldProduced(bazaar.Id, p, paying), 2, $"player {p}: bowing it produces 2 Gold");
            Eq(e.GoldProduced(token, p, paying), 1, "its Reaction produces 1 Gold");
            Check(e.PaymentOptions(p, 3, paying).Any(o => o.SourceIds.ToHashSet().SetEquals([bazaar.Id, token])), "3 Gold: the bow and the Reaction together");
            Check(!e.PaymentOptions(p, 4, paying).Any(), "never more than 3");
            Check(!e.PaymentOptions(q, 1, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip)).Any(o => o.SourceIds.Contains(token)), "the other player's payment never uses it");
            Reject(e, Equip(e, q, item, host, bazaar.Id, token));
            Accept(e, Equip(e, p, item, host, bazaar.Id, token));
            Eq(e.State.Cards[item.Id].AttachedTo, host.Id, "the Item was paid for and attached");
            Check(e.State.Cards[bazaar.Id].Bowed, "the bow bowed it");
            Eq(e.State.Cards[bazaar.Id].AbilityUses.GetValueOrDefault(ability), e.State.TurnNumber, "and the Reaction is spent for the turn");
            Check(!e.PaymentOptions(p, 1, new PaymentSubject(e.State.Cards[item2.Id], PaymentKind.Equip)).Any(), "nothing left: the bow and the Reaction are both used");
            // Alone, the Reaction pays 1 and leaves the Bazaar unbowed.
            var alone = Rig(false, out var b2, out var h2, out var i2, out _);
            var cheap = new PaymentSubject(alone.State.Cards[i2.Id], PaymentKind.Equip);
            Check(alone.PaymentOptions(p, 1, cheap).Any(o => o.SourceIds.SequenceEqual([PayToken(b2, "reaction")])), "the Reaction alone pays 1 Gold");
            var bowed = Rig(true, out var b3, out _, out var i3, out _);
            Check(!bowed.PaymentOptions(p, 1, new PaymentSubject(bowed.State.Cards[i3.Id], PaymentKind.Equip)).Any(), "a bowed Bazaar produces nothing, Reaction included");
            Check(h2.Id.Length > 0 && item2.Id.Length > 0 && host.Id.Length > 0 && b3.Id.Length > 0);
        }
    }

    private static void Zlkyt()
    {
        const string ability = "zlkyt-destroy-gold";
        var def = Def(ability);
        const string peddler = "oracle-l5r-8729", farm = "oracle-l5r-7915", mine = "oracle-l5r-9798";
        foreach (var p in new[] { 0, 1 })
        {
            var q = 1 - p;
            MatchEngine Rig(bool bowedZlkyt, bool hasFarm, out CardInstance zlkyt, out CardInstance farmCard, out CardInstance peddlerCard)
            {
                var s = Table(Phase.Action, p); s.PriorityPlayerId = p; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
                BowAllGold(s, 0); BowAllGold(s, 1);
                zlkyt = Add(s, def, p, Zone.Home, bowedZlkyt);
                farmCard = Add(s, farm, p, Zone.Home, bowed: true);
                if (!hasFarm) { s.Cards.Remove(farmCard.Id); }
                Add(s, mine, p, Zone.Home, bowed: true);
                peddlerCard = Add(s, peddler, p, Zone.Home);
                return MatchEngine.FromTestState(s);
            }
            var e = Rig(false, true, out var zlkyt, out var farmCard, out var ped);
            var draw = new GameCommand("useAbility", p, e.Version, CardId: ped.Id, AbilityId: "peddler-draw");
            var quote = e.QuoteAbilityGold(draw)!;
            var destroys = quote.Sources!.Where(x => x.Kind == "destroy").ToArray();
            var mineCard = e.State.Cards.Values.Single(c => c.DefinitionId == mine && c.OwnerId == p);
            Eq(destroys.Length, 2, $"player {p}: one option per Mine or Farm he controls");
            Check(destroys.All(x => x.CardId == zlkyt.Id && x.Produced == CardCatalog.Get(e.State.Cards[x.SubjectId!].DefinitionId).GoldCost), "each produces the Holding's base Gold Cost");
            Eq(destroys[0].SubjectId, mineCard.Id, "the Platinum Mine (6) first");
            // The other player has no Zlkyt: his quote never lists these.
            var theirs = e.State.Cards.Values.Where(c => c.OwnerId == q && c.DefinitionId == peddler).ToArray();
            Check(theirs.Length == 0, "the other player controls no Peddler in this rig");
            var hand = e.State.Players[p].Hand.Count;
            Reject(e, new GameCommand("useAbility", q, e.Version, CardId: ped.Id, AbilityId: "peddler-draw", SourceIds: [PayToken(zlkyt, "destroy:" + mineCard.Id)]));
            Reject(e, draw with { SourceIds = [PayToken(zlkyt, "destroy:" + farmCard.Id)] }); // the Farm's base Gold Cost (2) does not pay the draw's 3
            Accept(e, draw with { SourceIds = [PayToken(zlkyt, "destroy:" + mineCard.Id)] });
            Check(e.State.Cards[mineCard.Id].Zone != Zone.Home, "the (bowed) Mine was destroyed to pay");
            Eq(e.State.Players[p].Hand.Count, hand + 1, "and its base Gold Cost paid for the draw");
            Check(!e.State.Cards[zlkyt.Id].Bowed, "Zlkyt does not bow");
            Check(e.State.Cards[farmCard.Id].Zone == Zone.Home, "the other Holding stays");
            // Once a turn, not bowed, a Mine or Farm to destroy.
            bool Offered(MatchEngine m, CardInstance z, CardInstance pd) =>
                m.QuoteAbilityGold(new GameCommand("useAbility", p, m.Version, CardId: pd.Id, AbilityId: "peddler-draw"))?.Sources?.Any(x => x.CardId == z.Id) == true;
            Check(!Offered(e, zlkyt, ped), "once a turn: not offered again");
            var bowedZ = Rig(true, true, out var z2, out _, out var p2);
            Check(!Offered(bowedZ, z2, p2), "not while he is bowed");
            var noFarm = Rig(false, false, out var z3, out _, out var p3);
            noFarm = Batch336Support.Edit(noFarm, st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == p && c.DefinitionId == mine).ToArray()) c.Zone = Zone.DynastyDiscard; });
            z3 = noFarm.State.Cards[z3.Id]; p3 = noFarm.State.Cards[p3.Id];
            Check(!Offered(noFarm, z3, p3), "no Mine or Farm: no option");
        }
    }

    public static void Run(TestRunner runner) => runner.Run("single-reaction-5", [
        ("Ancient Armor of the Qamar: another player's action that would destroy the wearer opens the attack window; the Reaction negates his destruction (not the Armor's), only for its controller", QamarArmor),
        ("Bayushi Wakui: another player's action targeting a Magistrate at his battlefield targets Wakui instead; not for a non-Magistrate, from home, or when bowed", WakuiRedirect),
        ("Brothers of Goemon: with a Ring you control, another player's action on a card of the unit has its bowing negated; not without your Ring or outside the unit", BrothersOfGoemon),
        ("Chugo Seido: another player's action on your Personality is redirected to another of your Samurai at the same location, destroying the Holding", ChugoSeido),
        ("Halls of the Forgotten: another player's Battle action on your Berserker has its destruction delayed until the battle ends", HallsOfTheForgotten),
        ("Moto Shigeru: a Limited action on your Personality is negated; Shigeru permanently loses Personal Honor per other Personality and commits seppuku at 0", MotoShigeru),
        ("O-Yoroi Troops: another player's action on a card in the unit (or on the Follower) has its effects negated", ChampionOfThunderTroops),
        ("Shiba Sansesuke: bowing him redirects an action on your Shugenja at his location to him", ShibaSansesuke),
        ("Stone of Remembrance: removed from the game and torn to negate the destruction of one of your cards by another player's action", StoneOfRemembrance),
        ("The Castle of Order: once per battle another player's Battle action on a Ninja is redirected to another Ninja at the same location", CastleOfOrder),
        ("The Thunderer's Protection: an action on a card of the unit lets you bow the Spell to bow the action's performer afterwards", ThunderersProtection),
        ("Verdant Jungle: bowing the Region negates a movement of Personalities into an attacking army at its Province's battlefield", VerdantJungle),
        ("Training action negator: after another player's action targets your card, its effects are negated", TrainingActionNegator),
        ("Training mover negator: negates the movement of a Personality an action would move", TrainingMoverNegator),
        ("Training Honor-gain negator: an opponent-payment action's Honor gain is negated while both payments are made", TrainingHonorGainNegator),
        ("Training weapon breaker: before another player's action resolves, destroys the Weapons attached to its source", TrainingWeaponBreaker),
        ("Agasha Ryo: after he enters play from a Province, the Void Reaction refills it face up", AgashaRyo),
        ("Ashigaru Recruits: after it enters play from your hand, creates a 0F Ashigaru Follower attached to your target Personality", AshigaruRecruits),
        ("Daigotsu Kikumura: Invest 4 (2 for a Dragon player) creates two 1F Followers attached to him, once, only as he enters play", DaigotsuKikumura),
        ("Hiruma Koru: Invest 1 creates a 0F Follower attached to him, once, only as he enters play", HirumaKoru),
        ("Ikoma Jeiku: Invest 1 refills the Province he entered from face up, once, only as he enters play", IkomaJeiku),
        ("Isawa Hajime: Invest 3 gives him two +1F Fire tokens; he loses 1 Honor on entering play", IsawaHajime),
        ("Togashi Noritada: Invest 3 gives him two +1F Fire tokens, once, only as he enters play", TogashiNoritada),
        ("Kitsu Sorano: after she enters play, creates two 2F/2C/3PH Lion Ancestor Spirit Personalities", KitsuSorano),
        ("Kitsune Haruki: after he enters play, creates a 2F/2C/3PH Mantis Deer Spirit Personality with the Family Honor Limited", KitsuneHaruki),
        ("Kitsune Yamazaru: after you recruit him, creates a 3F/2C/3PH Mantis Monkey Spirit Personality with a Battle ability", KitsuneYamazaru),
        ("Singh Remnants: after your Personality enters play, attach it from hand or discard, paying its cost, and gain 1 Honor", SinghRemnants),
        ("Iuchi Takaki: after engaging, gives a target card a Madness token", IuchiTakaki),
        ("Lost Equipment: after engaging, bows a target enemy attachment at the battlefield", LostEquipment),
        ("Winter Sled: after engaging with enemy units there, bowing it moves its Personality to the battlefield", WinterSled),
        ("Utaku Ji-Yun: after engaging at her battlefield, a target opposing Personality may not use his abilities", UtakuJiYun),
        ("Fruitless Search: after engaging with enemy units there, creates a 4F/4C/3PH Imperial Magistrate that leaves after the battle", FruitlessSearch),
        ("Erosion: after an action moved a Personality to the current battlefield, he gets -4F", Erosion),
        ("Hida Defenders: after your action bowed, destroyed or moved a Personality who opposed it, gain 2 Honor", HidaDefenders),
        ("Matsu Rika: after your Bushido Virtue action resolves, +1F, three times per turn", MatsuRika),
        ("Rata: after an action Rata performed destroyed a Personality, draw a card", RataDraw),
        ("Shinjo Jong-Ho: after his Tactical action, bow a lower-Force opposing card", ShinjoJongHo),
        ("Kakita Tadanobu: after winning a duel, discard the Favor to draw two cards", KakitaTadanobu),
        ("Returning Home: after your Attack destroyed a province, create a bowed Holding and draw", ReturningHome),
        ("Seaside Bazaar: paying a Gold cost, its Reaction produces 1 Gold together with its bow", SeasideBazaar),
        ("Zlkyt: paying a Gold cost, destroy a Mine or Farm to produce its base Gold Cost", Zlkyt),
    ]);
}
