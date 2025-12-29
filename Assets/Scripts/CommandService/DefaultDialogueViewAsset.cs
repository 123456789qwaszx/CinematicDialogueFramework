using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "DefaultDialogueView", menuName = "Dialogue/DialogueView/Default")]
public class DefaultDialogueViewAsset : DialogueViewAsset
{
    public override IEnumerator ShowLine(DialogueLine line)
    {
        //UIDialogue ui = UIManager.Instance.GetUI<UIDialogue>();

        yield return null; //ui?.ShowLineCoroutine(line, ctx);
    }

    public override void ShowLineImmediate(DialogueLine line)
    {
        //UIDialogue ui = UIManager.Instance.GetUI<UIDialogue>();

        //ui?.ShowLineImmediate(line);
    }
}