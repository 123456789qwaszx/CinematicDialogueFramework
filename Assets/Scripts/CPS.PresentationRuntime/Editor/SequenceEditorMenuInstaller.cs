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
            menu.AddDisabledItem(new GUIContent("Recent/(empty)"));
        }
        else
        {
            foreach (var t in recent)
            {
                var tt = t; // 캡처 안전
                string label = GetDisplayLabel(tt);

                menu.AddItem(new GUIContent($"Recent/{label}"), false, () => onSingle(tt));
            }
        }
    }

    private static string GetDisplayLabel(Type t)
    {
        if (t == null) return "(null)";

        var hint = (CommandMenuHintAttribute)Attribute.GetCustomAttribute(t, typeof(CommandMenuHintAttribute));
        string label = hint?.DisplayName;

        if (string.IsNullOrWhiteSpace(label))
            label = t.Name;

        return label.Trim();
    }

}
#endif