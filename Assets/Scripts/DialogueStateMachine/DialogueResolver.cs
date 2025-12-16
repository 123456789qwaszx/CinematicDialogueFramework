using System.Collections.Generic;
using System.Linq;

public sealed class DialogueResolver
{
    private readonly IDialogueRouteCatalog _routes;

    public DialogueResolver(IDialogueRouteCatalog routes)
    {
        _routes = routes;
    }

    public (SituationEntry situation, DialogueRuntimeState state) ResolveNew(string situationKey)
    {
        if (!_routes.TryGetRoute(situationKey, out var route))
            return (null, null);

        if (route.Sequence == null || string.IsNullOrEmpty(route.SituationId))
            return (null, null);

        SituationEntry situation = FindSituation(route.Sequence, route.SituationId);
        if (situation == null || situation.nodes == null || situation.nodes.Count == 0)
            return (null, null);

        DialogueRuntimeState state = new()
        {
            SituationKey = situationKey,
            BranchKey    = "Default",
            VariantKey   = "Default",
            NodeCursor   = 0,
        };

        ResolveCurrentNodeGate(situation, state);
        return (situation, state);
    }

    private SituationEntry FindSituation(DialogueSequenceData seq, string situationId)
    {
        if (seq == null || seq.situations == null)
            return null;

        return seq.situations.FirstOrDefault(s =>
            s != null &&
            !string.IsNullOrEmpty(s.situationId) &&
            s.situationId == situationId);
    }

    public void ResolveCurrentNodeGate(SituationEntry situation, DialogueRuntimeState state)
    {
        state.Gate.Tokens      = new List<GateToken>();
        state.Gate.TokenCursor = 0;
        state.Gate.InFlight    = default;

        if (state.NodeCursor < 0 || situation.nodes == null || state.NodeCursor >= situation.nodes.Count)
            return;

        var node = situation.nodes[state.NodeCursor];

        if (node.gateTokens == null || node.gateTokens.Count == 0)
            state.Gate.Tokens.Add(GateToken.Immediately());
        else
            state.Gate.Tokens.AddRange(node.gateTokens);
    }

    public NodeViewModel BuildNodeViewModel(SituationEntry situation, DialogueRuntimeState state)
    {
        DialogueNodeSpec nodeSpec = situation.nodes[state.NodeCursor];

        return new NodeViewModel(
            state.SituationKey,
            state.NodeCursor,
            nodeSpec.line,
            state.BranchKey,
            state.VariantKey,
            state.CurrentNodeTokenCount
        );
    }
}
