using System.Collections.Generic;
using UnityEngine;

public sealed class ResolvedUIScreen
{
    public string ScreenId { get; }
    public UIScreenSpec BaseSpec { get; }

    public GameObject Prefab { get; }
    public ThemeSpec Theme { get; }
    public LayoutPatchSpec Layout { get; }

    public IReadOnlyList<string> AppliedVariantIds { get; }
    public string DecisionTrace { get; }

    public ResolvedUIScreen(
        string screenId,
        UIScreenSpec baseSpec,
        GameObject prefab,
        ThemeSpec theme,
        LayoutPatchSpec layout,
        List<string> appliedVariantIds,
        string decisionTrace)
    {
        ScreenId         = screenId;
        BaseSpec         = baseSpec;
        Prefab           = prefab;
        Theme            = theme;
        Layout           = layout;
        AppliedVariantIds = appliedVariantIds.AsReadOnly();
        DecisionTrace    = decisionTrace;
    }
}