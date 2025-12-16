using UnityEngine;
using System.Collections;

public interface IDialogueViewService
{
    IEnumerator ShowLine(DialogueLine line, CommandContext ctx);

    void ShowLineImmediate(DialogueLine line);
}

public abstract class DialogueViewAsset : ScriptableObject, IDialogueViewService
{
    public abstract IEnumerator ShowLine(DialogueLine line, CommandContext ctx);
    public abstract void ShowLineImmediate(DialogueLine line);
}