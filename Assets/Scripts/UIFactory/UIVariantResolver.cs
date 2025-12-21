using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class UIVariantResolver
{
    public ResolvedUIScreen Resolve(UIScreenSpec spec, in UIContext ctx)
    {
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));

        // 1) base
        GameObject prefab = spec.templatePrefab;
        ThemeSpec theme   = spec.baseTheme;
        LayoutPatchSpec layout = spec.baseLayout;

        List<string> applied = new List<string>(8);
        StringBuilder trace  = new StringBuilder(256);

        trace.AppendLine($"[UIResolve] screen={spec.screenId} basePrefab={(prefab != null ? prefab.name : "null")}");
        trace.AppendLine($"  ctx.theme={ctx.ThemeId}, ctx.locale={ctx.LocaleId}");

        // 2) debug override (screenId -> variantId)
        if (ctx.ScreenOverrides != null &&
            ctx.ScreenOverrides.TryGetValue(spec.screenId, out var forcedVariantId) &&
            !string.IsNullOrEmpty(forcedVariantId))
        {
            ApplyByVariantId(
                spec, forcedVariantId,
                ref prefab, ref theme, ref layout,
                applied, trace);

            return new ResolvedUIScreen(
                spec.screenId, spec, prefab, theme, layout, applied, trace.ToString());
        }

        // 3) normal rules (priority desc)
        var rules = spec.variants;
        if (rules != null && rules.Length > 0)
        {
            Array.Sort(rules, (a, b) => b.priority.CompareTo(a.priority));

            bool prefabLocked = false;

            foreach (var r in rules)
            {
                if (r == null || r.condition == null)
                    continue;

                if (!r.condition.Matches(ctx))
                    continue;

                trace.AppendLine($"  +match variant={r.variantId} (p={r.priority})");
                applied.Add(r.variantId);

                if (!prefabLocked && r.overridePrefab != null)
                {
                    prefab       = r.overridePrefab;
                    prefabLocked = true;
                    trace.AppendLine($"    prefab -> {prefab.name} (locked)");
                }

                if (r.overrideTheme != null)
                {
                    theme = r.overrideTheme; // 덮어쓰기 정책
                    trace.AppendLine($"    theme  -> {theme.name}");
                }

                if (r.overrideLayout != null)
                {
                    layout = r.overrideLayout; // 덮어쓰기 정책
                    trace.AppendLine($"    layout -> {layout.name}");
                }
            }
        }

        trace.AppendLine($"[UIResolve] result prefab={(prefab != null ? prefab.name : "null")}, theme={(theme ? theme.name : "null")}, layout={(layout ? layout.name : "null")}");

        return new ResolvedUIScreen(
            spec.screenId, spec, prefab, theme, layout, applied, trace.ToString());
    }

    private static void ApplyByVariantId(
        UIScreenSpec spec,
        string variantId,
        ref GameObject prefab,
        ref ThemeSpec theme,
        ref LayoutPatchSpec layout,
        List<string> applied,
        StringBuilder trace)
    {
        var rules = spec.variants;
        if (rules == null)
            return;

        foreach (var r in rules)
        {
            if (r == null)
                continue;

            if (r.variantId != variantId)
                continue;

            trace.AppendLine($"  [override] forced variant={variantId}");
            applied.Add(r.variantId);

            if (r.overridePrefab != null)
                prefab = r.overridePrefab;

            if (r.overrideTheme != null)
                theme = r.overrideTheme;

            if (r.overrideLayout != null)
                layout = r.overrideLayout;

            trace.AppendLine($"    forced result prefab={(prefab != null ? prefab.name : "null")}, theme={(theme ? theme.name : "null")}, layout={(layout ? layout.name : "null")}");
            return;
        }

        trace.AppendLine($"  [override] variantId not found: {variantId}");
    }
}
