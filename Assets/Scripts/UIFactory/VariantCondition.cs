using System;

//defines the exact scope in which a theme or variant should be applied
[Serializable]
public sealed class VariantCondition
{
    public string themeId;       // Ignored if null or empty
    public string localeId;      // Ignored if null or empty
    
    // Experiment (A/B test) identifier.
    // If set, this variant is applied only when the current UIContext
    // contains the given experiment key in its Experiments map.
    public string experimentKey; // Ignored if null or empty
    
    // Specific experiment variant value (e.g. "A", "B", "NewUI").
    // If set, this variant is applied only when the experiment's assigned
    // variant ID exactly matches this value.
    public string experimentVariantId; // Ignored if null or empty

    public bool Matches(in UIContext ctx)
    {
        if (!string.IsNullOrEmpty(themeId) && ctx.ThemeId != themeId)
            return false;

        if (!string.IsNullOrEmpty(localeId) && ctx.LocaleId != localeId)
            return false;

        if (!string.IsNullOrEmpty(experimentKey))
        {
            if (ctx.Experiments == null)
                return false;

            if (!ctx.Experiments.TryGetValue(experimentKey, out string v))
                return false;

            if (!string.IsNullOrEmpty(experimentVariantId) && v != experimentVariantId)
                return false;
        }

        return true;
    }
}