#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CommandMenuUtility
{
    private sealed class MenuItemInfo
    {
        public Type Type;
        public CommandMenuHintAttribute Hint;

        public string Category;
        public string Label;

        public string[] Sets;
        public int SetOrder;
        public int Order;
    }

    private static List<MenuItemInfo> BuildItems(IReadOnlyList<Type> allTypes)
    {
        if (allTypes == null) return new List<MenuItemInfo>();

        return allTypes
            .Where(t =>
                t != null &&
                !t.IsAbstract &&
                !t.IsGenericType &&
                !t.ContainsGenericParameters)
            .Select(t =>
            {
                var hint = t.GetCustomAttribute<CommandMenuHintAttribute>();

                string category = (hint?.Category ?? "Other").Trim();
                if (string.IsNullOrEmpty(category)) category = "Other";

                string label = (hint?.DisplayName ?? t.Name).Trim();
                if (string.IsNullOrEmpty(label)) label = t.Name;

                return new MenuItemInfo
                {
                    Type     = t,
                    Hint     = hint,
                    Category = category,
                    Label    = label,
                    Sets     = hint?.Sets,
                    SetOrder = hint?.SetOrder ?? 0,
                    Order    = hint?.Order ?? 0
                };
            })
            .ToList();
    }

    // ---------------------------
    // 1) Sets Menu (top)
    // ---------------------------
    public static void BuildSetsMenu(
        GenericMenu menu,
        IReadOnlyList<Type> allTypes,
        Action<Type> onSelectedSingle,
        Action<IReadOnlyList<Type>> onSelectedSet)
    {
        if (menu == null) throw new ArgumentNullException(nameof(menu));

        var items = BuildItems(allTypes);
        if (items.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No CommandSpecBase types found"));
            return;
        }

        var setMap = new Dictionary<string, List<MenuItemInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var it in items)
        {
            if (it.Sets == null || it.Sets.Length == 0)
                continue;

            for (int i = 0; i < it.Sets.Length; i++)
            {
                string raw = it.Sets[i];
                string setPath = (raw ?? "").Trim();
                if (string.IsNullOrEmpty(setPath))
                    continue;

                if (!setMap.TryGetValue(setPath, out var list))
                {
                    list = new List<MenuItemInfo>();
                    setMap[setPath] = list;
                }
                list.Add(it);
            }
        }

        if (setMap.Count == 0)
            return;

        // Sort set folders by path
        foreach (var kv in setMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            string setPath = kv.Key;

            // Sort items within a set
            var list = kv.Value
                .OrderBy(x => x.SetOrder)
                .ThenBy(x => x.Order)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Add "Add All"
            string addAllPath = $"{setPath}/(Add All {list.Count})";
            menu.AddItem(new GUIContent(addAllPath), false, () =>
            {
                var types = list.Select(x => x.Type).ToList();
                onSelectedSet?.Invoke(types);
            });

            // Add each command entry
            foreach (var item in list)
            {
                var captured = item;
                string singlePath = $"{setPath}/{captured.Label}";
                menu.AddItem(new GUIContent(singlePath), false, () =>
                {
                    onSelectedSingle?.Invoke(captured.Type);
                });
            }

            // separator inside this folder
            menu.AddSeparator(setPath + "/");
        }
    }

    // ---------------------------
    // 2) Category Menu (bottom)
    // ---------------------------
    public static void BuildCategoryMenu(
        GenericMenu menu,
        IReadOnlyList<Type> allTypes,
        Action<Type> onSelectedSingle)
    {
        if (menu == null) throw new ArgumentNullException(nameof(menu));

        var items = BuildItems(allTypes);
        if (items.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No CommandSpecBase types found"));
            return;
        }

        // Group by category; "Other" always last
        var groups = items
            .GroupBy(i => string.IsNullOrEmpty(i.Category) ? "Other" : i.Category)
            .OrderBy(g => string.Equals(g.Key, "Other", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var g in groups)
        {
            foreach (var i in g
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase))
            {
                var captured = i;
                string path = $"{g.Key}/{captured.Label}";
                menu.AddItem(new GUIContent(path), false, () =>
                {
                    onSelectedSingle?.Invoke(captured.Type);
                });
            }
        }
    }
}
#endif
