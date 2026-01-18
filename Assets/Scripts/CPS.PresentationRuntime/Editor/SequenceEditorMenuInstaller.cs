#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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

                // 1) Sets
                CommandMenuUtility.BuildSetsMenu(menu, allTypes, onSingle, onBatch);

                // separator if sets exist (GenericMenu는 item count를 못 알아서 그냥 넣어도 무방)
                menu.AddSeparator("");

                // 2) Recent
                AddRecentSection(menu, allTypes, onSingle);
                menu.AddSeparator("");

                // 3) Category
                CommandMenuUtility.BuildCategoryMenu(menu, allTypes, onSingle);

                // 4) Extension
                extendMenu?.Invoke(menu);

                menu.ShowAsContext();
                return true;
            };
    }

    private static void AddRecentSection(GenericMenu menu, IReadOnlyList<Type> allTypes, Action<Type> onSingle)
    {
        // 네 프로젝트의 레지스트리 구현을 그대로 사용한다고 가정
        var recent = CommandRecentRegistry.GetRecentTypes(allTypes);

        if (recent == null || recent.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("Recent/(empty)"));
            return;
        }

        foreach (var t in recent)
        {
            var tt = t;
            string label = GetDisplayLabel(tt);
            menu.AddItem(new GUIContent($"Recent/{label}"), false, () => onSingle(tt));
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
