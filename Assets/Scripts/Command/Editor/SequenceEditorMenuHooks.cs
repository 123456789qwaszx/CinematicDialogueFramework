#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SequenceEditorMenuHooks
{
    /// <summary>
    /// 외부(도메인)에서 메뉴 구성을 가로채고 싶을 때 등록하는 델리게이트.
    /// 
    /// - commandTypes   : 모든 커맨드 타입 목록
    /// - onAddRequested : 특정 타입 선택 시 호출해야 하는 콜백
    /// - extendMenu     : 에디터 쪽에서 넘긴 공통 메뉴 주입용 콜백(Delete 등)
    /// 
    /// 반환값:
    /// - true  : 도메인이 메뉴 표시까지 다 처리했다는 뜻
    /// - false : 에디터가 기본 메뉴로 fallback 해야 한다는 뜻
    /// </summary>
    public delegate bool ShowCommandMenuHandler(
        List<Type> commandTypes,
        Action<Type> onAddRequested,
        Action<GenericMenu> extendMenu);

    /// <summary>
    /// 선택적으로 설정되는 외부 훅.
    /// Lab 쪽에서 설치해서 커맨드 메뉴를 커스터마이징할 수 있음.
    /// </summary>
    public static ShowCommandMenuHandler ShowCommandMenu;

    /// <summary>
    /// SequenceSpecEditorWindow 등에서 호출하는 진입점.
    /// 
    /// 1) ShowCommandMenu 가 설정되어 있으면 그쪽에 위임
    /// 2) 아니면 기본 GenericMenu + extendMenu 로 구성
    /// </summary>
    public static bool TryShowCommandMenu(
        List<Type> commandTypes,
        Action<Type> onAddRequested,
        Action<GenericMenu> extendMenu = null)
    {
        if (commandTypes == null || commandTypes.Count == 0)
            return false;

        // 1) 도메인에서 직접 처리하겠다고 등록한 경우
        if (ShowCommandMenu != null)
        {
            return ShowCommandMenu(commandTypes, onAddRequested, extendMenu);
        }

        // 2) 훅이 없으면 에디터 기본 메뉴로 fallback
        var menu = new GenericMenu();

        // 기본: 타입 이름으로 Add 메뉴 구성
        foreach (var t in commandTypes)
        {
            var captured = t;
            menu.AddItem(new GUIContent(captured.Name), false, () =>
            {
                onAddRequested?.Invoke(captured);
            });
        }

        // 에디터에서 넘긴 공통 항목(Delete 등) 주입
        extendMenu?.Invoke(menu);

        menu.ShowAsContext();
        return true;
    }
}
#endif