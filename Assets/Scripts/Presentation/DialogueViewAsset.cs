using UnityEngine;
using System.Collections;

public interface IDialogueViewService
{
    // ✅ 여기서의 코루틴은 "타이핑/페이드/컷인 등 연출이 끝날 때까지"만 기다린다.
    //    절대 플레이어 입력(다음 진행)을 기다리면 안 된다.
    //    진행 조건(입력/딜레이/신호)은 전부 GateRunner + GateToken이 담당한다.
    IEnumerator ShowLine(DialogueLine line);

    // 즉시 완성 상태로 보여주는 함수 (스킵 등에서 사용)
    void ShowLineImmediate(DialogueLine line);
}

public abstract class DialogueViewAsset : ScriptableObject, IDialogueViewService
{
    public abstract IEnumerator ShowLine(DialogueLine line);
    public abstract void ShowLineImmediate(DialogueLine line);
}