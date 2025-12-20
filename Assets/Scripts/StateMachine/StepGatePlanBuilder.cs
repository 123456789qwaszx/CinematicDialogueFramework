using System.Collections.Generic;
using UnityEngine;

public class StepGatePlanBuilder
{
    public void BuildForCurrentNode(SituationSpecSO situation, ref DialogueRuntimeState state)
    {
        var gate = new StepGateState
        {
            StepGates      = new List<GateToken>(8),
            StepIndex = 0,
            InFlight    = default
        };

        if (state == null || situation == null || situation.nodes == null)
        {
            gate.StepGates.Add(GateToken.Immediately());
            if (state != null) state.Gate = gate;
            return;
        }

        // End-of-situation: 최소 1 토큰 보장
        if (state.NodeCursor >= situation.nodes.Count)
        {
            gate.StepGates.Add(GateToken.Immediately());
            state.Gate = gate;
            return;
        }

        DialogueNodeSpec node = situation.nodes[state.NodeCursor];

        if (node == null || node.steps == null || node.steps.Count == 0)
        {
            gate.StepGates.Add(GateToken.Input());
            state.Gate = gate;
            return;
        }

        for (int i = 0; i < node.steps.Count; i++)
        {
            DialogueStepSpec step = node.steps[i];

            // step 자체가 null이거나 gate가 default면 Input으로 보정
            GateToken token = (step == null) ? default : step.gate;
            if (EqualityComparer<GateToken>.Default.Equals(token, default))
                token = GateToken.Input();

            gate.StepGates.Add(token);
        }

        if (gate.StepGates.Count == 0)
            gate.StepGates.Add(GateToken.Input());

        state.Gate = gate;
    }
}