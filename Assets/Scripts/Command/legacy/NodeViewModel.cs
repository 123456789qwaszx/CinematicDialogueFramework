// public readonly struct NodeViewModel
// {
//     public readonly string SituationKey;
//     public readonly int NodeIndex;
//     public readonly int StepIndex;
//
//     public readonly string SpeakerId;
//     public readonly string Text;
//     public readonly Expression Expression;
//     public readonly DialoguePosition Position;
//
//     public readonly string BranchKey;
//     public readonly string VariantKey;
//
//     public readonly int StepGateTokenCount;
//
//     public NodeViewModel(
//         string situationKey,
//         int nodeIndex,
//         int stepIndex,
//         string speakerId,
//         string text,
//         Expression expression,
//         DialoguePosition position,
//         string branchKey,
//         string variantKey,
//         int stepGateTokenCount)
//     {
//         SituationKey       = situationKey ?? "(none)";
//         NodeIndex          = nodeIndex;
//         StepIndex          = stepIndex;
//         SpeakerId          = speakerId ?? "";
//         Text               = text ?? "";
//         Expression         = expression;
//         Position           = position;
//         BranchKey          = branchKey ?? "Default";
//         VariantKey         = variantKey ?? "Default";
//         StepGateTokenCount = stepGateTokenCount;
//     }
//
//     public static NodeViewModel FallbackFromState(DialogueRuntimeState state, string message)
//     {
//         return new NodeViewModel(
//             situationKey: state?.SituationKey ?? "(none)",
//             nodeIndex: state?.CurrentNodeIndex ?? -1,
//             stepIndex: state?.StepGate.StepIndex ?? -1,
//             speakerId: "",
//             text: message ?? "",
//             expression: Expression.Default,
//             position: DialoguePosition.Left,
//             branchKey: state?.BranchKey ?? "Default",
//             variantKey: state?.VariantKey ?? "Default",
//             stepGateTokenCount: state?.StepGateTokenCount ?? 0
//         );
//     }
// }