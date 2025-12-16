using System.Collections.Generic;

public sealed class DialogueResolver
{
    private readonly IDialogueRouteCatalog _routes;

    public DialogueResolver(IDialogueRouteCatalog routes)
    {
        _routes = routes;
    }

    public (DialogueSituationSpec spec, DialogueRuntimeState state) ResolveNew(string situationKey)
    {
        if (!_routes.TryGetRoute(situationKey, out var route))
            return (null, null);

        // Resolver는 상태머신 Spec만 해석한다.
        if (route.Kind != DialogueRouteKind.StateMachine || route.StateMachineSpec == null)
            return (null, null);

        DialogueSituationSpec situationSpec = route.StateMachineSpec;

        DialogueRuntimeState state = new()
        {
            SituationKey = situationKey,
            BranchKey = "Default",
            VariantKey = "Default",
            NodeCursor = 0,
        };

        ResolveCurrentNodeGate(situationSpec, state);
        return (situationSpec, state);
    }

    public void ResolveCurrentNodeGate(DialogueSituationSpec situationSpec, DialogueRuntimeState state)
    {
        state.Gate.Tokens = new List<GateToken>();
        state.Gate.TokenCursor = 0;
        state.Gate.InFlight = default;

        if (state.NodeCursor < 0 || state.NodeCursor >= situationSpec.nodes.Count)
            return;

        var node = situationSpec.nodes[state.NodeCursor];

        if (node.gateTokens == null || node.gateTokens.Count == 0)
            state.Gate.Tokens.Add(GateToken.Immediately());
        else
            state.Gate.Tokens.AddRange(node.gateTokens);
    }

    public NodeViewModel BuildNodeViewModel(DialogueSituationSpec situationSpec, DialogueRuntimeState state)
    {
        DialogueNodeSpec nodeSpec = situationSpec.nodes[state.NodeCursor];

        return new NodeViewModel(
            state.SituationKey,
            state.NodeCursor,
            nodeSpec.speakerId,
            nodeSpec.text,
            state.BranchKey,
            state.VariantKey,
            state.CurrentNodeTokenCount
        );
    }
}
