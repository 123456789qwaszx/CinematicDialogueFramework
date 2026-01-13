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

        var items = allTypes
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

        // ------------------------------------------------------------
        // 0) 세트 메뉴: Custom/PortraitStart 같은 “매크로 + 구성원” 메뉴
        // ------------------------------------------------------------
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

        if (setMap.Count > 0)
        {
            foreach (var kv in setMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                string setPath = kv.Key;

                var list = kv.Value
                    .OrderBy(x => x.SetOrder)
                    .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // 0-1) 전체 세트를 한 번에 추가하는 항목
                // 예) Custom/PortraitStart/(Add All 5)
                string addAllPath = $"{setPath}/(Add All {list.Count})";
                menu.AddItem(new GUIContent(addAllPath), false, () =>
                {
                    var types = list.Select(x => x.Type).ToList();
                    onSelectedSet?.Invoke(types);
                });

                // 0-2) 이 세트에 포함된 개별 커맨드들도 같이 노출
                // 예) Custom/PortraitStart/Slide In
                foreach (var item in list)
                {
                    var captured = item;
                    string singlePath = $"{setPath}/{captured.Label}";
                    menu.AddItem(new GUIContent(singlePath), false, () =>
                    {
                        onSelectedSingle?.Invoke(captured.Type);
                    });
                }

                // 세트 내 구분선 (세트별 subtree 안에서)
                menu.AddSeparator(setPath + "/");
            }

            // 세트 블록과 아래 카테고리 블록 사이 전역 구분선
            menu.AddSeparator("");
        }

        // ------------------------------------------------------------
        // 1) Category 메뉴(기본)
        // ------------------------------------------------------------
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
}
#endif
