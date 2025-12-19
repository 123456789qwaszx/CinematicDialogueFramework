using System.Collections.Generic;

public class DialogueGatePlanner
{
    public void BuildCurrentNodeGate(SituationSpec situation, ref DialogueRuntimeState state)
    {
        var gate = new GateCursor
        {
            Tokens      = new List<GateToken>(8),
            TokenCursor = 0,
            InFlight    = default
        };
        
        // End-of-situation: 최소 1 토큰 보장
        if (state.NodeCursor >= situation.nodes.Count)
        {
            gate.Tokens.Add(GateToken.Immediately());
            state.Gate = gate;
            return;
        }

        DialogueNodeSpec node = situation.nodes[state.NodeCursor];

        if (node.gateTokens != null && node.gateTokens.Count > 0)
            gate.Tokens.AddRange(node.gateTokens);
        else
            gate.Tokens.Add(GateToken.Input());

        state.Gate = gate;
    }
}