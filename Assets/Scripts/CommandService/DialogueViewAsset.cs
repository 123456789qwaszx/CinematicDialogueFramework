using UnityEngine;
using System.Collections;

public interface IDialogueViewService
{
    IEnumerator ShowLine(DialogueLine line, string screenId, string widgetId);

    void ShowLineImmediate(DialogueLine line, string screenId, string widgetId);
}

public abstract class DialogueViewAsset : ScriptableObject, IDialogueViewService
{
    public abstract IEnumerator ShowLine(DialogueLine line, string screenId, string widgetId);
    public abstract void ShowLineImmediate(DialogueLine line, string screenId, string widgetId);
}