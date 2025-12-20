using System.Collections.Generic;

public class NodeViewModelBuilder
{
    public NodeViewModel Build(SituationSpecSO situation, DialogueRuntimeState state)
    {
        // 생존성 가드
        if (state == null)
            return NodeViewModel.System("(none)", -1, "State is null.");

        if (situation == null || situation.nodes == null)
            return NodeViewModel.System(state.SituationKey, state.NodeCursor, $"Missing SituationSpec: '{state.SituationKey}'");

        if (state.NodeCursor < 0 || state.NodeCursor >= situation.nodes.Count)
            return NodeViewModel.System(state.SituationKey, state.NodeCursor, $"Invalid NodeCursor={state.NodeCursor}");

        var node = situation.nodes[state.NodeCursor];
        if (node == null || node.steps == null || node.steps.Count == 0)
        {
            return new NodeViewModel(
                state.SituationKey,
                state.NodeCursor,
                string.Empty,
                string.Empty,
                Expression.Default,
                DialoguePosition.Left,
                state.BranchKey,
                state.VariantKey,
                state.CurrentNodeTokenCount
            );
        }

        // 핵심: "현재 step"에서 대표 라인 찾기
        int stepIndex = state.Gate.StepIndex; // GateCursor.TokenCursor == 현재 step index 라는 전제
        DialogueLine line = null;

        if (stepIndex >= 0 && stepIndex < node.steps.Count)
        {
            var step = node.steps[stepIndex];
            line = FindPrimaryLine(step?.commands);
        }

        // fallback: 현재 step에 라인이 없으면, 노드 전체 steps에서 첫 라인 탐색
        if (line == null)
            line = FindPrimaryLine(node);

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

    private static DialogueLine FindPrimaryLine(DialogueNodeSpec node)
    {
        if (node?.steps == null) return null;

        for (int i = 0; i < node.steps.Count; i++)
        {
            var step = node.steps[i];
            var line = FindPrimaryLine(step?.commands);
            if (line != null) return line;
        }
        return null;
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
