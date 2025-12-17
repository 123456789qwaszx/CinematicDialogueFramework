using System.Collections.Generic;
using System.Linq;

public class DialogueResolver
{
    private readonly IDialogueRouteCatalog _routes;

    public DialogueResolver(IDialogueRouteCatalog routes)
    {
        _routes = routes;
    }

    public (SituationSpec situation, DialogueRuntimeState state) Resolve(string routeKey)
    {
        DialogueRoute route = _routes.GetRoute(routeKey);
        DialogueSequenceData sequence = route.Sequence;
        
        SituationSpec situation = sequence.GetSituation(route.SituationKey);
        
        DialogueRuntimeState state = new()
        {
            SituationKey = routeKey,
            BranchKey    = "Default",
            VariantKey   = "Default",
            NodeCursor   = 0,
        };

        ResolveCurrentNodeGate(situation, state);
        return (situation, state);
    }

    public void ResolveCurrentNodeGate(SituationSpec situation, DialogueRuntimeState state)
    {
        state.Gate.Tokens      = new List<GateToken>();
        state.Gate.TokenCursor = 0;
        state.Gate.InFlight    = default;

        if (state.NodeCursor < 0 || situation.nodes == null || state.NodeCursor >= situation.nodes.Count)
            return;

        DialogueNodeSpec node = situation.nodes[state.NodeCursor];

        if (node.gateTokens == null || node.gateTokens.Count == 0)
            state.Gate.Tokens.Add(GateToken.Immediately());
        else
            state.Gate.Tokens.AddRange(node.gateTokens);
    }

    public NodeViewModel BuildNodeViewModel(SituationSpec situation, DialogueRuntimeState state)
    {
        DialogueNodeSpec nodeSpec = situation.nodes[state.NodeCursor];

        IReadOnlyList<NodeCommandSpec> commandSpecs =
            nodeSpec.commands != null
                ? nodeSpec.commands
                : System.Array.Empty<NodeCommandSpec>();

        DialogueLine primaryLine = FindPrimaryLine(commandSpecs);

        return new NodeViewModel(
            state.SituationKey,
            state.NodeCursor,
            commandSpecs,
            primaryLine,
            state.BranchKey,
            state.VariantKey,
            state.CurrentNodeTokenCount
        );
    }

    private static DialogueLine FindPrimaryLine(IReadOnlyList<NodeCommandSpec> specs)
    {
        if (specs == null) return null;

        // 1) 첫 번째 ShowLine 커맨드 찾기
        for (int i = 0; i < specs.Count; i++)
        {
            NodeCommandSpec spec = specs[i];
            if (spec == null) continue;

            if (spec.kind == NodeCommandKind.ShowLine && spec.line != null)
                return spec.line;
        }

        // 2) 없어도 최소한 null 반환 (Presenter 쪽에서 방어)
        return null;
    }
}
