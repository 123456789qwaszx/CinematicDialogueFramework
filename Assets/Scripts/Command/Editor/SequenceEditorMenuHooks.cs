#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class SequenceEditorMenuHooks
{
    /// <summary>
    /// 외부(도메인)에서 Command 메뉴를 커스터마이즈하기 위한 훅.
    /// 반환값: true면 메뉴를 처리했다는 뜻, false면 기본 메뉴로 fallback.
    /// 매개변수:
    ///   allTypes    : CommandSpecBase 파생타입 전체 목록
    ///   onSelected  : 사용자가 어떤 타입을 선택했을 때 호출할 콜백
    /// </summary>
    public static Func<IReadOnlyList<Type>, Action<Type>, bool> ShowCommandMenu;
    
    public static bool TryShowCommandMenu(IReadOnlyList<Type> allTypes, Action<Type> onSelected)
    {
        if (ShowCommandMenu == null)
            return false;

        try
        {
            return ShowCommandMenu(allTypes, onSelected);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            return false;
        }
    }
}
#endif