using System.Collections.Generic;

public class StepGatePlanBuilder
{
    public void BuildForCurrentNode(SequenceSpecSO situation, SequenceProgressState state)
    {
        var gate = new StepGateState
        {
            Tokens      = new List<GateToken>(8),
            StepIndex = 0,
            InFlight    = default
        };

        if (state == null || situation == null || situation.nodes == null)
        {
            gate.Tokens.Add(GateToken.Immediately());
            if (state != null) state.StepGate = gate;
            return;
        }

        // End-of-situation: 최소 1 토큰 보장
        if (state.CurrentNodeIndex >= situation.nodes.Count)
        {
            gate.Tokens.Add(GateToken.Immediately());
            state.StepGate = gate;
            return;
        }

        NodeSpec node = situation.nodes[state.CurrentNodeIndex];

        if (node == null || node.steps == null || node.steps.Count == 0)
        {
            gate.Tokens.Add(GateToken.Input());
            state.StepGate = gate;
            return;
        }

        for (int i = 0; i < node.steps.Count; i++)
        {
            StepSpec step = node.steps[i];

            // step 자체가 null이거나 gate가 default면 Input으로 보정
            GateToken token = (step == null) ? default : step.gate;
            if (EqualityComparer<GateToken>.Default.Equals(token, default))
                token = GateToken.Input();

            gate.Tokens.Add(token);
        }

        if (gate.Tokens.Count == 0)
            gate.Tokens.Add(GateToken.Input());

        state.StepGate = gate;
    }
}