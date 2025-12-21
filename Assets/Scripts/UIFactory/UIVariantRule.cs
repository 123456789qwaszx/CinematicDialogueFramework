using System;
using UnityEngine;

[Serializable]
public class UIVariantRule
{
    public string variantId; // e.g. "Shop_Layout_B"
    public int priority = 0; // Higher value = higher priority

    public VariantCondition condition;

    // What this rule overrides
    public GameObject overridePrefab;       // Replaces the screen prefab (only if specified)
    public ThemeSpec overrideTheme;          // Overrides the theme
    public LayoutPatchSpec overrideLayout;   // Overrides layout / locale-specific settings
}
