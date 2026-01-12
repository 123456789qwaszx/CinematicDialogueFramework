using System.Collections.Generic;

public class StepGatePlanBuilder
{
    public void BuildForCurrentNode(SequenceSpecSO sequence, SequenceProgressState state)
    {
        var gate = new StepGateState
        {
            Tokens   = new List<GateToken>(8),
            Cursor   = 0,
            InFlight = default
        };

        // Fallback: in an invalid state. Add one Immediate so Advancer can run once and exit safely.
        if (state == null || sequence == null || sequence.nodes == null)
        {
            gate.Tokens.Add(GateToken.Immediately());
            if (state != null) state.StepGate = gate;
            return;
        }

        // End of sequence: keep a single Immediate so Tick() can reach Session.End() cleanly.
        if (state.NodeIndex >= sequence.nodes.Count)
        {
            gate.Tokens.Add(GateToken.Immediately());
            state.StepGate = gate;
            return;
        }

        NodeSpec node = sequence.nodes[state.NodeIndex];

        if (node == null || node.steps == null || node.steps.Count == 0)
        {
            gate.Tokens.Add(GateToken.Input());
            state.StepGate = gate;
            return;
        }

        for (int i = 0; i < node.steps.Count; i++)
        {
            StepSpec step = node.steps[i];

            GateToken token = step?.gate ?? GateToken.Immediately();
            gate.Tokens.Add(token);
        }

        state.StepGate = gate;
    }
}