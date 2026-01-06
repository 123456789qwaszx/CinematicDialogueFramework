// using UnityEngine;
// using System.Collections;
//
// [CreateAssetMenu(fileName = "DefaultDialogueView", menuName = "Dialogue/DialogueView/Default")]
// public class DefaultDialogueViewAsset : DialogueViewAsset
// {
//     public override IEnumerator ShowLine(DialogueLine line, string screedId, string widgetId)
//     {
//         Debug.Log($"[DialogueBootstrap] ShowLine (target='{screedId}_{widgetId}', speaker='{line?.speakerId}', text='{line?.text}')");
//         //UIDialogue ui = UIManager.Instance.GetUI<UIDialogue>();
//
//         yield return null; //ui?.ShowLineCoroutine(line, ctx);
//     }
//
//     public override void ShowLineImmediate(DialogueLine line, string screedId, string widgetId )
//     {
//         Debug.Log($"[DialogueBootstrap] ShowLineImmediate (target='{screedId}_{widgetId}', speaker='{line?.speakerId}', text='{line?.text}')");
//         //UIDialogue ui = UIManager.Instance.GetUI<UIDialogue>();
//
//         //ui?.ShowLineImmediate(line);
//     }
// }