namespace L5R.Core;

// P10 (issue #695): the live conditional keyword grants that Keyword() used to carry as branches, now catalogue rules. Each is the same test as the branch it replaced, read at the same moment
// (Keyword() consults the registry before the printed and granted keywords, and every one of these only ever added a keyword, so the order of the additions does not matter):
//   naval-while-has-spell            Isawa Kojiro: "Kojiro has Naval while he has a Spell."
//   unit-followers-cavalry           Moto Jun-Ni: "Followers in this unit have Cavalry."
//   cavalry-force-while-not-your-turn  Tamori Yayu: "has Cavalry while it is not your turn."
//   watari-sole-follower-cavalry     Hida Watari: "While Watari has exactly one Follower, that Follower has Cavalry."
// Kojiro's branch returned its answer outright, so a later source could never add Naval to HIM; none can (every later source names its own card Effect or other keywords), so the rule is the same.
public sealed partial class MatchEngine
{
    internal static class LegacyKeywordRules
    {
        internal const string KojiroNaval = "P10-naval-while-has-spell", JunNiCavalry = "P10-unit-followers-cavalry", YayuCavalry = "P10-cavalry-while-not-your-turn", WatariCavalry = "P10-watari-sole-follower-cavalry";
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterLegacyKeywordRules() => KeywordRuleCatalog.Register(
        new(LegacyKeywordRules.KojiroNaval, KeywordRuleScope.Self, ["Naval"], SourceEffect: "naval-while-has-spell", AllOf: [new(KeywordConditionKind.SubjectHasAttachmentOfType, nameof(CardType.Spell))]),
        new(LegacyKeywordRules.JunNiCavalry, KeywordRuleScope.Unit, ["Cavalry"], SourceEffect: "unit-followers-cavalry",
            AllOf: [new(KeywordConditionKind.SubjectIsType, nameof(CardType.Follower)), new(KeywordConditionKind.SubjectAttached)]),
        new(LegacyKeywordRules.YayuCavalry, KeywordRuleScope.Self, ["Cavalry"], SourceEffect: "cavalry-force-while-not-your-turn", AllOf: [new(KeywordConditionKind.NotControllersTurn)]),
        new(LegacyKeywordRules.WatariCavalry, KeywordRuleScope.Unit, ["Cavalry"], SourceEffect: WatariEffect,
            AllOf: [new(KeywordConditionKind.SubjectIsType, nameof(CardType.Follower)), new(KeywordConditionKind.SubjectAttached), new(KeywordConditionKind.SourceFollowerCount, "1")]));
}
