using System.Collections.Generic;

public sealed class DialogueResolver
{
    private readonly DialogueCatalog _catalog;

    public DialogueResolver(DialogueCatalog catalog)
    {
        _catalog = catalog;
    }

    public (DialogueSituationSpec spec, DialogueRuntimeState state) ResolveNew(string situationKey)
    {
        DialogueSituationSpec situationSpec = _catalog.Get(situationKey);
        if (situationSpec == null) return (null, null);

        DialogueRuntimeState state = new ()
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