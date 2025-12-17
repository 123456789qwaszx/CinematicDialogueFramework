using System.Collections.Generic;

public class NodeViewModelBuilder
{
    public NodeViewModel Build(SituationSpec situation, DialogueRuntimeState state)
    {
        var node = situation.nodes[state.NodeCursor];
        var line = FindPrimaryLine(node.commands);

        return new NodeViewModel(
            state.SituationKey,
            state.NodeCursor,
            line?.speakerId ?? string.Empty,
            line?.text ?? string.Empty,
            line?.expression ?? Expression.Default,
            line?.position ?? DialoguePosition.Left,
            state.BranchKey,
            state.VariantKey,
            state.CurrentNodeTokenCount
        );
    }

    private static DialogueLine FindPrimaryLine(IReadOnlyList<NodeCommandSpec> specs)
    {
        if (specs == null) return null;

        for (int i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            if (spec != null && spec.kind == NodeCommandKind.ShowLine && spec.line != null)
                return spec.line;
        }
        return null;
    }
}