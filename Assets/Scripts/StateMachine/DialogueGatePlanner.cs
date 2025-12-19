using System.Collections.Generic;
using UnityEngine;

public class DialogueGatePlanner
{
    public void BuildCurrentNodeGate(SituationSpecSO situation, ref DialogueRuntimeState state)
    {
        var gate = new GateCursor
        {
            Tokens      = new List<GateToken>(8),
            TokenCursor = 0,
            InFlight    = default
        };

        if (state == null || situation == null || situation.nodes == null)
        {
            gate.Tokens.Add(GateToken.Immediately());
            if (state != null) state.Gate = gate;
            return;
        }

        // End-of-situation: 최소 1 토큰 보장
        if (state.NodeCursor >= situation.nodes.Count)
        {
            gate.Tokens.Add(GateToken.Immediately());
            state.Gate = gate;
            return;
        }

        DialogueNodeSpec node = situation.nodes[state.NodeCursor];

        if (node == null || node.steps == null || node.steps.Count == 0)
        {
            gate.Tokens.Add(GateToken.Input());
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

            gate.Tokens.Add(token);
        }

        if (gate.Tokens.Count == 0)
            gate.Tokens.Add(GateToken.Input());

        state.Gate = gate;
    }
}