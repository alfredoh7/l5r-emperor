// Fixture for tools/lint_comment_swallow.py: ordinary code this lint must NOT flag.
namespace L5RFixtures.CommentSwallow;

public sealed class Clean
{
    // A prose comment that names a real method plus parens and a semicolon, on a
    // comment-only line (no code precedes the //). Distinguishes ordinary
    // documentation prose from actually-swallowed code, which always has real
    // code before the // on the same line.
    // reader asks is HasReconnaissanceAtCurrentBattlefield(player); the raw form below is kept for parity.
    public int? AttackStrengthChain(string abilityId) =>
        MechCAttackStrength(abilityId) ?? EmperorP1AttackStrength(abilityId) ?? P2AttackStrength(abilityId);

    // An ordinary trailing attribution comment with no chain operator before it.
    public void Straighten(CardInstance target) => target.Bowed = false; // issue #492 ruling R1

    // A chain whose comment sits on its OWN following line, not appended to the
    // chain's own physical line -- nothing here is at risk of being swallowed.
    public int? OtherChain(string abilityId) =>
        MechCAttackStrength(abilityId)
        ?? EmperorP1AttackStrength(abilityId)
        // this comment cannot swallow anything: it is not on the same line as any chain term
        ?? 0;
}
