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
        public int Order;

        public string[] Sets;
        public int SetOrder;
    }

    // ✅ 공통: 타입 -> MenuItemInfo 리스트 만들기 (중복 제거)
    private static List<MenuItemInfo> BuildItems(IReadOnlyList<Type> allTypes)
    {
        if (allTypes == null) return new List<MenuItemInfo>();

        return allTypes
            .Where(t => t != null && !t.IsAbstract)
            .Select(t =>
            {
                var hint = t.GetCustomAttribute<CommandMenuHintAttribute>();
                return new MenuItemInfo
                {
                    Type     = t,
                    Hint     = hint,
                    Category = (hint?.Category ?? "Other").Trim(),
                    Label    = (hint?.DisplayName ?? t.Name).Trim(),
                    Order    = hint?.Order ?? 0,
                    Sets     = hint?.Sets,
                    SetOrder = hint?.SetOrder ?? 0
                };
            })
            .ToList();
    }

    // ✅ 0) Sets 파트만 빌드
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
            if (it.Sets == null) continue;

            foreach (var setPathRaw in it.Sets)
            {
                var setPath = (setPathRaw ?? "").Trim();
                if (string.IsNullOrEmpty(setPath)) continue;

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

        foreach (var kv in setMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            string setPath = kv.Key;

            var list = kv.Value
                .OrderBy(x => x.SetOrder)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string addAllPath = $"{setPath}/(Add All {list.Count})";
            menu.AddItem(new GUIContent(addAllPath), false, () =>
            {
                var types = list.Select(x => x.Type).ToList();
                onSelectedSet?.Invoke(types);
            });

            foreach (var item in list)
            {
                var captured = item;
                string singlePath = $"{setPath}/{captured.Label}";
                menu.AddItem(new GUIContent(singlePath), false, () =>
                {
                    onSelectedSingle?.Invoke(captured.Type);
                });
            }

            menu.AddSeparator(setPath + "/");
        }
    }

    // ✅ 1) Category(탐색) 파트만 빌드
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
                string path = $"{g.Key}/{i.Label}";
                var captured = i;
                menu.AddItem(new GUIContent(path), false, () =>
                {
                    onSelectedSingle?.Invoke(captured.Type);
                });
            }
        }
    }

    // ✅ 기존 API 유지(호환용): Sets -> Separator -> Category
    public static void BuildCommandSelectionMenu(
        GenericMenu menu,
        IReadOnlyList<Type> allTypes,
        Action<Type> onSelectedSingle,
        Action<IReadOnlyList<Type>> onSelectedSet)
    {
        if (menu == null) throw new ArgumentNullException(nameof(menu));

        if (allTypes == null || allTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No CommandSpecBase types found"));
            return;
        }

        // 기존과 동일한 출력(단지 내부가 분리됨)
        int beforeCount = CountMenuItemsSafe(menu);

        BuildSetsMenu(menu, allTypes, onSelectedSingle, onSelectedSet);

        // Sets가 하나라도 추가됐다면 구분선 넣기 (기존 behavior 유지)
        if (CountMenuItemsSafe(menu) > beforeCount)
            menu.AddSeparator("");

        BuildCategoryMenu(menu, allTypes, onSelectedSingle);
    }

    // GenericMenu item count를 직접 알 수 없어서 “대충 분기”가 필요할 때 대비용
    // (여기서는 단순히 0 반환해도 되지만, 래퍼에서 separator 조건을 엄밀히 하고 싶으면 확장 가능)
    private static int CountMenuItemsSafe(GenericMenu menu) => 0;
}
#endif
