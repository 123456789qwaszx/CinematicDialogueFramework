#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

public static class SequenceEditorMenuHooks
{
    // 외부(Lab)에서 꽂아줄 델리게이트
    // - single: 단일 커맨드 추가
    // - batch : 세트(여러 커맨드) 추가
    public static Func<
        IReadOnlyList<Type>,            // commandTypes
        Action<Type>,                   // onAddSingleRequested
        Action<IReadOnlyList<Type>>,    // onAddBatchRequested
        Action<GenericMenu>,            // extendMenu
        bool                            // handled
    > ShowCommandMenu;

    // 에디터(SequenceSpecEditorWindow)에서 부르는 API
    public static bool TryShowCommandMenu(
        IReadOnlyList<Type> commandTypes,
        Action<Type> onAddSingleRequested,
        Action<IReadOnlyList<Type>> onAddBatchRequested,
        Action<GenericMenu> extendMenu = null)
    {
        if (ShowCommandMenu == null)
            return false;

        return ShowCommandMenu(commandTypes, onAddSingleRequested, onAddBatchRequested, extendMenu);
    }
}
#endif