// using UnityEngine;
//
// public interface IDialogueNodeOutput
// {
//     void Hide();
//     void ShowSystemMessage(string msg);
//     void PlayStep(NodeSpec node, int stepIndex, CommandRunScope scope, DialogueLine fallbackLine = null);
//     void FinishStep();
// }
//
// public sealed class DialogueNodeOutputComposite : IDialogueNodeOutput
// {
//     private readonly INodeExecutor _executor;
//
//     public DialogueNodeOutputComposite(INodeExecutor executor)
//     {
//         _executor = executor;
//     }
//     
//     public void PlayStep(NodeSpec node, int stepIndex, CommandRunScope scope, DialogueLine fallbackLine = null)
//     {
//         _executor?.PlayStep(node, stepIndex, scope, fallbackLine);
//     }
//
//     public void FinishStep()
//     {
//         _executor.FinishStep();
//     }
// }
