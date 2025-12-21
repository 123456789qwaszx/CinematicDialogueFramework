using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UIScreenSpec
{
    public string screenId;
    
    public ThemeSpec baseTheme;          // nullable
    public LayoutPatchSpec baseLayout;   // nullable

    public UIVariantRule[] variants;     // nullable
    
    
    public string name;                  // 디스플레이용 이름
    public GameObject templatePrefab;    // 실제 UI 프리팹

    // 슬롯 이름 -> WidgetSpec 리스트
    public List<SlotSpec> slots = new();
}

[Serializable]
public class SlotSpec
{
    public string slotName; // "Header" / "Body" / "Footer"
    public List<WidgetSpec> widgets = new();
}

[Serializable]
public sealed class WidgetSpec
{
    public WidgetType widgetType;
    public string text;
    public string onClickRoute;
}

public enum WidgetType { Text, Button }
public enum ScreenKey { Home, Shop }

[CreateAssetMenu(menuName = "UI/Screen Catalog", fileName = "UIScreenCatalog")]
public class UIScreenCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ScreenKey key;
        public UIScreenSpec spec;
    }

    [Header("Registered Screens")]
    public List<Entry> entries = new();

    // 런타임용 캐시 (에셋에 저장되지 않음)
    private Dictionary<ScreenKey, UIScreenSpec> _map;

    private void OnEnable()
    {
        BuildCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 값 바뀔 때도 다시 빌드
        BuildCache();
    }
#endif

    private void BuildCache()
    {
        _map = new Dictionary<ScreenKey, UIScreenSpec>();

        foreach (var e in entries)
        {
            if (e == null || e.spec == null) continue;
            _map[e.key] = e.spec;
        }
    }

    public UIScreenSpec GetScreenSpec(ScreenKey key)
    {
        if (_map == null)
            BuildCache();

        if (_map.TryGetValue(key, out var spec))
            return spec;

        throw new KeyNotFoundException($"UIScreenSpec not found for key: {key}");
    }
}
