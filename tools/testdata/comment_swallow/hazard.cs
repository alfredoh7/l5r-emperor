// Fixture for tools/lint_comment_swallow.py. Reconstructs two real hazard shapes
// this project actually hit, rather than a synthetic strawman:
//   1. The historical bug (issue #498): a real statement's trailing comment had
//      a whole extra call appended after it on the same physical line, so the
//      call compiled fine and simply never ran. See
//      docs/decisions/2026-09-25-rend-no-hiding-place-ring-search.md and commit
//      16d2ca6b (merged as 506a4c6b) for the actual before/after diff.
//   2. The proactive risk shape named in the Rung 3 task: a ??/||/.Concat chain
//      ending right before a trailing comment, exactly as it appears today
//      (safely, for now) at src/L5R.Core/CardAbilities.cs:4762.
namespace L5RFixtures.CommentSwallow;

public sealed class Hazards
{
    // Reconstructs issue #498's actual broken line (RingEnteredByOwnText), with
    // the card-agnostic names swapped in for a self-contained fixture.
    public void RingRecruitReconstruction(Player player, int turnNumber, CardInstance source)
    {
        player.RingPutIntoPlayTurn = turnNumber; // Brotherhood Schism (issue #453, batch 2) RingEnteredByOwnText(source!); // issue #492: a Ring put into play by its own text counts for Enlightenment again
    }

    // Reconstructs the real, currently-safe CardAbilities.cs:4762 shape: fine as
    // written, but a future appended term must go before the comment, not after.
    public int AttackStrengthChain(string abilityId) =>
        MechCAttackStrength(abilityId) ?? EmperorP1AttackStrength(abilityId) ?? P2AttackStrength(abilityId) ?? 0; // issue #503 P1; Emperor Edition mechanics group C (issue #501)
}
