#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SequenceEditorMenuInstaller
{
    static SequenceEditorMenuInstaller()
    {
        SequenceEditorMenuHooks.ShowCommandMenu =
            (allTypes, onSingle, onBatch, extendMenu) =>
            {
                var menu = new GenericMenu();

                // 1) Sets (top)
                CommandMenuUtility.BuildSetsMenu(menu, allTypes, onSingle, onBatch);
                menu.AddSeparator("");

                // 2) Recent (middle)
                AddRecentSection(menu, allTypes, onSingle);
                menu.AddSeparator("");

                // 3) Category / Search (bottom)  ← 지금은 Category만
                CommandMenuUtility.BuildCategoryMenu(menu, allTypes, onSingle);

                // 4) Common extension (Delete 등)
                extendMenu?.Invoke(menu);

                menu.ShowAsContext();
                return true;
            };
    }

    private static void AddRecentSection(GenericMenu menu, IReadOnlyList<Type> allTypes, Action<Type> onSingle)
    {
        var recent = CommandRecentRegistry.GetRecentTypes(allTypes);

        if (recent.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("Recent/(No recent commands yet)"));
            return;
        }

        foreach (var t in recent)
        {
            var tt = t;
            menu.AddItem(new GUIContent($"Recent/{tt.Name}"), false, () => onSingle(tt));
        }
    }
}
#endif