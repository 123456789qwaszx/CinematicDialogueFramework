#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public static class SequenceEditorMenuInstaller
{
    static SequenceEditorMenuInstaller()
    {
        SequenceEditorMenuHooks.ShowCommandMenu =
            (allTypes, onSingle, onBatch, extendMenu) =>
            {
                var menu = new GenericMenu();

                var recent = CommandRecentRegistry.GetRecentTypes(allTypes);
                // Lab 전용: Sets + Category (Favorites 제거)
                CommandMenuUtility.BuildCommandSelectionMenu(
                    menu,
                    allTypes,
                    onSelectedSingle: onSingle,
                    onSelectedSet: onBatch
                );

                // 에디터 공통 메뉴(Delete 등) 주입
                extendMenu?.Invoke(menu);

                menu.ShowAsContext();
                return true;
            };
    }
}
#endif