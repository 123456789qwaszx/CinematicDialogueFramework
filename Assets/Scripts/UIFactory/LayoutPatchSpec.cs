using UnityEngine;

public class LayoutSpecPatch : IUIPatch
{
    private readonly LayoutPatchSpec _layout;

    public LayoutSpecPatch(LayoutPatchSpec layout)
    {
        _layout = layout;
    }

    public void Apply(UIScreen screen)
    {
        // TODO: Add support for locale-specific layouts, padding, and alignment
        UnityEngine.Debug.Log($"LayoutSpecPatch applied: {_layout.name}");
    }
}

[CreateAssetMenu(menuName = "UI/LayoutPatchSpec")]
public class LayoutPatchSpec : ScriptableObject
{
    // Reserved for future locale-specific line breaks, font sizes, and layout padding

    public void BuildPatches(System.Collections.Generic.List<IUIPatch> patches)
    {
        patches.Add(new LayoutSpecPatch(this));
    }
}