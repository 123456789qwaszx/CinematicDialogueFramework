using System.Collections;

public interface IPresentationPort
{
    void ShakeCamera(float strength, float duration);
    IEnumerator ShowLine(DialogueLine line);
    void ShowLineImmediate(DialogueLine line);
}