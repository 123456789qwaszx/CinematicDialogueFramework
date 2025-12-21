using System.Collections.Generic;

public sealed class NodeViewModelBuilder
{
    public NodeViewModel Build(SituationSpecSO situation, DialogueRuntimeState state)
    {
        int nodeIndex = state.CurrentNodeIndex;
        int stepIndex = state.StepGate.StepIndex;

        DialogueNodeSpec node = situation.nodes[nodeIndex];

        DialogueLine line = GetPrimaryLine(node, stepIndex);

        return new NodeViewModel(
            situationKey: state.SituationKey,
            nodeIndex: nodeIndex,
            stepIndex: stepIndex,
            speakerId: line?.speakerId ?? string.Empty,
            text: line?.text ?? string.Empty,
            expression: line?.expression ?? Expression.Default,
            position: line?.position ?? DialoguePosition.Left,
            branchKey: state.BranchKey,
            variantKey: state.VariantKey,
            stepGateTokenCount: state.StepGateTokenCount
        );
    }

    private DialogueLine GetPrimaryLine(DialogueNodeSpec node, int stepIndex)
    {
        // 1) current step first
        if (node.steps != null && stepIndex >= 0 && stepIndex < node.steps.Count)
        {
            List<NodeCommandSpec> commands = node.steps[stepIndex]?.commands;
            DialogueLine line = FirstShowLine(commands);
            
            if (line != null)
                return line;
        }

        // 2) fallback: scan all steps
        if (node.steps != null)
        {
            for (int step = 0; step < node.steps.Count; step++)
            {
                List<NodeCommandSpec> commands = node.steps[step]?.commands;
                DialogueLine line = FirstShowLine(commands);
                
                if (line != null)
                    return line;
            }
        }

        return null;
    }

    private DialogueLine FirstShowLine(List<NodeCommandSpec> commands)
    {
        if (commands == null)
            return null;

        for (int i = 0; i < commands.Count; i++)
        {
            NodeCommandSpec spec = commands[i];
            if (spec != null && spec.kind == NodeCommandKind.ShowLine && spec.line != null)
                return spec.line;
        }
        
        return null;
    }
}
