using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Dark Experiments ("Choose a province"): Core lists one eventChoice per live province, Choice "<owner>:<index>" with Provinces [index]. Clicking that province on the table
    // selects the option exactly as picking its row does; Confirm choice still commits it. Core validates the answer, the client only presents the engine's legal provinces.
    private ActionOption? EventProvinceOption(int owner, int province) =>
        GenericDecisionActive && View.PendingChoice is { Type: "eventChoice" } pending && pending.PlayerId == View.ViewerId && View.PriorityPlayerId == View.ViewerId
            ? Actions.FirstOrDefault(a => a.Enabled && a.Type == "eventChoice" && a.Provinces is [var only] && only == province && a.Command?.Choice == $"{owner}:{province}")
            : null;

    private bool EventProvinceChoiceActive => Actions.Any(a => a.Type == "eventChoice" && a.Provinces is { Length: 1 }) && GenericDecisionActive && View.PendingChoice is { Type: "eventChoice" };

    private bool EventProvinceChosen(int owner, int province) =>
        _decisionSelection is { Type: "eventChoice", Provinces: [var only], Command: { } command } && only == province && command.Choice == $"{owner}:{province}";

    private bool SelectEventProvince(int owner, int province)
    {
        if (EventProvinceOption(owner, province) is not { } option) return false;
        SelectCandidateOption(option);
        return true;
    }
}
