using System.Collections.Generic;

public class DialogueGatePlanner
{
    public void BuildCurrentNodeGate(SituationSpec situation, ref DialogueRuntimeState state)
    {
        state.Gate.Tokens      = new List<GateToken>();
        state.Gate.TokenCursor = 0;
        state.Gate.InFlight    = default;

        if (state.NodeCursor >= situation.nodes.Count)
        {// End-of-situation
            state.Gate.Tokens.Add(GateToken.Immediately());
            state.Gate.TokenCursor = 0;
            return;
        }

        DialogueNodeSpec node = situation.nodes[state.NodeCursor];
        state.Gate.Tokens.AddRange(node.gateTokens);
    }
}