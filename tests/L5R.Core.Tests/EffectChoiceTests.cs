using static RegressionAssertions;
using static Support337;
using L5R.Core;

// The conditional effect choice (issue #337, EffectChoice.cs) and Mirumoto Yumaru (oracle-l5r-5181), its first card: "Battle: Target an enemy Personality: If Yumaru
// has a Weapon, you may move her home and you may move the Personality home. Otherwise, move both of them home." Mendon's ruling: with a Weapon the player chooses
// ONE of THREE effects (Yumaru home, the target home, both); without one there is no choice and both go home. The condition is read when the action RESOLVES.
// Every reading (and the open question about declining both) is in docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md. Player 0 acts.
internal static class EffectChoiceTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Mirumoto Yumaru is the printed card (Force 4, Chi 3, Cost 7, Personal Honor 2, no Honor Requirement, Dragon Kensai Samurai), whole -- one Battle ability whose branch tests for a Weapon, offers exactly three options and otherwise moves both home", PublishedShape),
            ("Without a Weapon there is no decision: the action resolves at once, both Yumaru and the target go home, nothing is asked and priority passes", NoWeaponMovesBothHome),
            ("With a Weapon the action pauses for the acting player: three engine-listed options (her, the target, both) and nothing else, the opponent is only told to wait, and each option applies exactly its own effects", WeaponOpensAThreeWayChoice),
            ("The choice is validated: only the acting player, only a listed option; \"neither\", nothing, another command or a stale version leave everything unchanged", TheChoiceIsValidated),
            ("The condition is read when the action RESOLVES, not when it is announced: a Weapon a before-action reaction destroys means no choice and both go home", ConditionIsReadAtResolution),
            ("A movement negated by a before-action reaction (Tamori Tamoko) stays negated through the deferred choice, with a Weapon (chosen 'both') and without (the automatic branch)", NegationSurvivesTheChoice),
            ("The action finishes like any other once chosen: an additional action taken this way keeps priority while more remain, an ordinary one hands it over, and after-resolution traits fire after the choice, not before", FinishesLikeAnyOtherAction),
            ("Only a Weapon counts (an Item, an Armor or a Follower does not), and it must be attached to Yumaru herself", OnlyHerOwnWeaponCounts),
            ("The target must be an enemy Personality at the battlefield; Yumaru herself must be there", TargetAndPresence),
            ("Every offered choice validates on a fresh engine built from the paused state", OfferedChoicesValidate),
        ];
        runner.Run("effect-choice", tests);
    }

    private const string Ability = "yumaru-move-home";

    private sealed record Rig(MatchState S, CardInstance Yumaru, CardInstance Enemy, CardInstance? Weapon);

    private static Rig Battle(bool weapon)
    {
        var s = Table(Phase.Battle);
        var yumaru = Put(s, Support337.Yumaru, 0, Zone.Battlefield, province: 0);
        var enemy = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var w = weapon ? Put(s, "training-weapon", 0, Zone.Attached, province: 0, parent: yumaru.Id) : null;
        return new(s, yumaru, enemy, w);
    }

    private static string[] ChoiceIds(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice!).ToArray();

    private static void PublishedShape()
    {
        var d = CardCatalog.Get(Support337.Yumaru);
        Expect(!d.ScenarioOnly && d.Type == CardType.Personality && d.Name == "Mirumoto Yumaru" && d.Clan == "Dragon");
        // Verified against the scan: Force 4, Chi 3, Honor Requirement 0, Gold Cost 7, Personal Honor 2.
        Eq(d.Force, 4); Eq(d.Chi, 3); Eq(d.GoldCost, 7); Eq(d.PersonalHonor, 2); Eq(d.HonorRequirement, 0);
        foreach (var k in new[] { "Kensai", "Dragon Clan", "Samurai" }) Expect(d.Keywords!.Contains(k), k);
        Eq(d.Text, "Battle: Target an enemy Personality: If Yumaru has a Weapon, you may move her home and you may move the Personality home. Otherwise, move both of them home.");
        Expect(d.Abilities!.SequenceEqual([Ability]), "one ability"); Eq(d.Effect, "none");
        var a = AbilityCatalog.All[Ability];
        Eq(a.Timing, "Battle"); Eq(a.Target, "enemy-personality");
        Eq(a.Branch!.Condition, BranchCondition.SourceHasWeapon);
        Expect(a.Branch.Options.Select(o => o.Id).SequenceEqual(["self", "target", "both"]), "three options: her, the target, both");
        Expect(a.Branch.Options[0].Effects.SequenceEqual([BranchEffect.MoveSourceHome]) && a.Branch.Options[1].Effects.SequenceEqual([BranchEffect.MoveTargetHome])
            && a.Branch.Options[2].Effects.SequenceEqual([BranchEffect.MoveSourceHome, BranchEffect.MoveTargetHome]) && a.Branch.Otherwise.Effects.SequenceEqual([BranchEffect.MoveSourceHome, BranchEffect.MoveTargetHome]));
        Expect(ArtShipped(Support337.Yumaru), "art is shipped"); Eq(d.ArtworkKey, $"res://Content/Published/{Support337.Yumaru}.jpg");
    }

    private static void NoWeaponMovesBothHome()
    {
        var r = Battle(weapon: false);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
        var s = e.State;
        Expect(s.PendingChoice is null && s.PendingEffectChoice is null, "no decision was opened");
        Eq(s.Cards[r.Yumaru.Id].Zone, Zone.Home); Eq(s.Cards[r.Enemy.Id].Zone, Zone.Home);
        Expect(s.Cards[r.Yumaru.Id].ProvinceIndex is null && s.Cards[r.Enemy.Id].ProvinceIndex is null, "both are home");
        Expect(!s.Cards[r.Enemy.Id].Bowed && !s.Cards[r.Yumaru.Id].Bowed, "moving home does not bow");
        Eq(s.PriorityPlayerId, 1, "priority passed");
        Expect(s.Events.Count(x => x.Type == "unitMovedHome") == 2, "both moves are in the log");
    }

    private static void WeaponOpensAThreeWayChoice()
    {
        var r = Battle(weapon: true);
        var e = Engine(r.S);
        var command = Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id);
        Accept(e, command);
        var s = e.State;
        Eq(s.PendingChoice?.Type, "effectChoice"); Eq(s.PendingChoice?.PlayerId, 0); Expect(s.PendingEffectChoice is not null);
        Eq(s.PriorityPlayerId, 0, "the acting player decides");
        Expect(s.Cards[r.Yumaru.Id].Zone == Zone.Battlefield && s.Cards[r.Enemy.Id].Zone == Zone.Battlefield, "nothing has moved yet");
        Expect(ChoiceIds(e, 0).SequenceEqual(["self", "target", "both"]), "exactly the three engine-listed options, in order");
        var actions = e.QueryActions(0).Where(a => a.Type == "chooseEffect").ToArray();
        Expect(actions.All(a => a.Enabled && a.CardId == r.Yumaru.Id && a.Command!.Type == "chooseEffect" && a.Command.PlayerId == 0), "complete commands anchored on Yumaru");
        Expect(actions.Select(a => a.Label).SequenceEqual(["Move Yumaru home", "Move the targeted Personality home", "Move both home"]), "each says what it does");
        Expect(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the other player is only told to wait");
        Expect(e.GetView(1).PendingChoice is { Type: "effectChoice" }, "the pending decision itself is public");
        foreach (var (choice, yumaruHome, targetHome) in new[] { ("self", true, false), ("target", false, true), ("both", true, true) })
        {
            var fresh = Engine(s);
            Accept(fresh, actions.Single(a => a.Command!.Choice == choice).Command!);
            var after = fresh.State;
            Eq(after.Cards[r.Yumaru.Id].Zone == Zone.Home, yumaruHome, $"{choice}: Yumaru");
            Eq(after.Cards[r.Enemy.Id].Zone == Zone.Home, targetHome, $"{choice}: the target");
            Expect(after.Cards[r.Weapon!.Id].AttachedTo == r.Yumaru.Id && (after.Cards[r.Weapon.Id].ProvinceIndex is null) == yumaruHome, $"{choice}: her Weapon moves with her unit");
            Expect(after.PendingChoice is null && after.PendingEffectChoice is null, $"{choice}: the decision closed");
            Eq(after.PriorityPlayerId, 1, $"{choice}: priority passed");
            Expect(after.Events.Any(x => x.Type == "effectChosen"), $"{choice}: the choice is in the public log");
        }
    }

    private static void TheChoiceIsValidated()
    {
        var r = Battle(weapon: true);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
        var choose = new GameCommand("chooseEffect", 0, e.Version, Choice: "both");
        Reject(e, choose with { Choice = "none" });                       // "neither" is not offered (Mendon's three-option ruling)
        Reject(e, choose with { Choice = null });
        Reject(e, choose with { Choice = "" });
        Reject(e, choose with { Choice = "BOTH" });
        Reject(e, choose with { PlayerId = 1 });                          // not their decision (and not their priority)
        Reject(e, choose with { ExpectedVersion = e.Version - 1 });
        Reject(e, new GameCommand("pass", 0, e.Version));
        Reject(e, new GameCommand("declineReaction", 0, e.Version));
        Reject(e, new GameCommand("useAbility", 0, e.Version, r.Yumaru.Id, TargetId: r.Enemy.Id, AbilityId: Ability));
        Expect(e.State.PendingEffectChoice is not null && e.State.Cards[r.Yumaru.Id].Zone == Zone.Battlefield, "every refusal left the paused action untouched");
        Accept(e, choose);
        Reject(e, choose with { ExpectedVersion = e.Version });          // and it cannot be answered twice
    }

    private static void ConditionIsReadAtResolution()
    {
        var r = Battle(weapon: true);
        var breaker = Put(r.S, "training-weapon-breaker", 1, Zone.Battlefield, province: 0);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action is deferred behind the reaction window, the Weapon still attached");
        Expect(e.State.Cards[r.Weapon!.Id].Zone == Zone.Attached);
        Accept(e, Offer(e, 1, "training-break-weapon", breaker.Id));
        Eq(e.State.Cards[r.Weapon.Id].Zone, Zone.FateDiscard, "the reaction destroyed her Weapon");
        while (e.State.PendingChoice?.Type == "beforeActionReaction")
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).Single(a => a.Type == "declineReaction").Command!);
        var s = e.State;
        Expect(s.PendingChoice is null && s.PendingEffectChoice is null, "she no longer has a Weapon: no choice is asked");
        Expect(s.Cards[r.Yumaru.Id].Zone == Zone.Home && s.Cards[r.Enemy.Id].Zone == Zone.Home, "both went home");
        // Control: without the reaction the same announcement does open the choice.
        var control = Battle(weapon: true); Put(control.S, "training-weapon-breaker", 1, Zone.Battlefield, province: 0);
        var ce = Engine(control.S);
        Accept(ce, Offer(ce, 0, Ability, control.Yumaru.Id, control.Enemy.Id));
        while (ce.State.PendingChoice?.Type == "beforeActionReaction")
            Accept(ce, ce.QueryActions(ce.State.PriorityPlayerId).Single(a => a.Type == "declineReaction").Command!);
        Eq(ce.State.PendingChoice?.Type, "effectChoice", "everyone passed: she still has the Weapon, so the choice opens");
    }

    private static void NegationSurvivesTheChoice()
    {
        foreach (var weapon in new[] { true, false })
        {
            var r = Battle(weapon);
            var tamoko = Put(r.S, "training-tamoko", 1, Zone.Battlefield, province: 0);
            var e = Engine(r.S);
            Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "Tamoko may negate a movement of the target");
            Accept(e, Offer(e, 1, "tamoko-negate", tamoko.Id));
            while (e.State.PendingChoice?.Type == "beforeActionReaction")
                Accept(e, e.QueryActions(e.State.PriorityPlayerId).Single(a => a.Type == "declineReaction").Command!);
            if (weapon)
            {
                Eq(e.State.PendingChoice?.Type, "effectChoice", "the choice opens after the window");
                Expect(e.State.PendingEffectChoice!.Negated.Any(n => n.Kind == "move" && n.CardId == r.Enemy.Id), "the negation was carried into the paused action");
                Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "both").Command!);
            }
            var s = e.State;
            Eq(s.Cards[r.Yumaru.Id].Zone, Zone.Home, "Yumaru's own move is not negated");
            Eq(s.Cards[r.Enemy.Id].Zone, Zone.Battlefield, $"the target's move stays negated ({(weapon ? "chosen" : "automatic")} branch)");
            Expect(s.NegatedEffects.Count == 0, "and no negation leaks past the action");
            Expect(s.Events.Any(x => x.Type == "moveNegated"), "the negation is in the public log");
        }
    }

    private static void FinishesLikeAnyOtherAction()
    {
        // An additional action (Again!'s shape): a grant is consumed by taking the action; with another still unused the acting player keeps priority.
        foreach (var (grants, staysWithActor) in new[] { (0, false), (1, false), (2, true) })
        {
            var r = Battle(weapon: true);
            for (var i = 0; i < grants; i++) r.S.AdditionalActions.Add(new(0, null));
            var e = Engine(r.S);
            Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
            Eq(e.State.AdditionalActions.Count, Math.Max(0, grants - 1), $"{grants} grants: taking the action used one");
            Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "target").Command!);
            Eq(e.State.PriorityPlayerId, staysWithActor ? 0 : 1, $"{grants} grants: who holds priority afterwards");
            Eq(e.State.AdditionalActions.Count, Math.Max(0, grants - 1), "and the remaining grant is intact");
        }
        // An ordinary action forfeits nothing here (a grant it did not use is forfeited only by a non-grant action, exactly as for any action).
        var traits = Battle(weapon: true);
        var ebizo = Put(traits.S, "oracle-l5r-1629", 0, Zone.Battlefield, province: 0);
        traits.Yumaru.GrantedKeywords.Add(new("Recon", "test", "Permanent"));       // her action is then a Recon action (an ability inherits its card's keywords)
        var te = Engine(traits.S);
        var force = te.GetView(0).Players[0].Army.Single(c => c.Id == ebizo.Id).Force;
        Accept(te, Offer(te, 0, Ability, traits.Yumaru.Id, traits.Enemy.Id));
        Eq(te.GetView(0).Players[0].Army.Single(c => c.Id == ebizo.Id).Force, force, "the action has not resolved: Ebizo's after-resolution trait has not fired");
        Accept(te, te.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "self").Command!);
        Eq(te.GetView(0).Players[0].Army.Single(c => c.Id == ebizo.Id).Force, force + 1, "it fires when the chosen effects have applied");
    }

    private static void OnlyHerOwnWeaponCounts()
    {
        foreach (var (definition, onYumaru, choice) in new[] { ("training-item", true, false), ("training-armor", true, false), ("training-follower", true, false), ("training-weapon", false, false), ("training-weapon", true, true), ("training-kama", true, true), ("training-unique-weapon", true, true) })
        {
            var r = Battle(weapon: false);
            var host = onYumaru ? r.Yumaru : Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Put(r.S, definition, 0, Zone.Attached, province: 0, parent: host.Id);
            var e = Engine(r.S);
            Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
            Eq(e.State.PendingChoice?.Type == "effectChoice", choice, $"{definition} attached to {(onYumaru ? "Yumaru" : "another Personality")}");
        }
    }

    private static void TargetAndPresence()
    {
        var r = Battle(weapon: false);
        var enemyFollower = Put(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var enemyHome = Put(r.S, "training-crane-samurai", 1, Zone.Home);
        var own = Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = Engine(r.S);
        var targets = Offers(e, 0, Ability).Single(o => o.Enabled).TargetIds!;
        Expect(targets.SequenceEqual([r.Enemy.Id]), $"only the enemy Personality at the battlefield, got {string.Join(", ", targets)}");
        foreach (var bad in new[] { enemyFollower.Id, enemyHome.Id, own.Id, r.Yumaru.Id })
            Reject(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id) with { TargetId = bad });
        var away = Edit(e, st => { st.Cards[r.Yumaru.Id].Zone = Zone.Home; st.Cards[r.Yumaru.Id].ProvinceIndex = null; });
        Expect(Offers(away, 0, Ability).All(o => !o.Enabled), "Yumaru herself must be at the battlefield");
    }

    private static void OfferedChoicesValidate()
    {
        var r = Battle(weapon: true);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, Ability, r.Yumaru.Id, r.Enemy.Id));
        OfferedCommandsValidate(e, () => Engine(e.State));
    }
}
