using System.Collections.Generic;

public sealed class NodeViewModelBuilder
{
    public NodeViewModel Build(SequenceSpecSO situation, DialogueRuntimeState state)
    {
        int nodeIndex = state.CurrentNodeIndex;
        int stepIndex = state.StepGate.StepIndex;

        NodeSpec node = situation.nodes[nodeIndex];

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

    private DialogueLine GetPrimaryLine(NodeSpec node, int stepIndex)
    {
        // 1) current step first
        if (node.steps != null && stepIndex >= 0 && stepIndex < node.steps.Count)
        {
            List<CommandSpecBase> commands = node.steps[stepIndex]?.commands;
            DialogueLine line = FirstShowLine(commands);
            
            if (line != null)
                return line;
        }

        // 2) fallback: scan all steps
        if (node.steps != null)
        {
            for (int step = 0; step < node.steps.Count; step++)
            {
                List<CommandSpecBase> commands = node.steps[step]?.commands;
                DialogueLine line = FirstShowLine(commands);
                
                if (line != null)
                    return line;
            }
        }

        return null;
    }

    private DialogueLine FirstShowLine(List<CommandSpecBase> commands)
    {
        if (commands == null || commands.Count == 0)
            return null;

        for (int i = 0; i < commands.Count; i++)
        {
            CommandSpecBase spec = commands[i];
            if (spec is DefaultShowLineCommandSpec show && show.line != null)
                return show.line;
        }

        return null;
    }
}
