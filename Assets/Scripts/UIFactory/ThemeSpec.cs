using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ThemeSpecPatch : IUIPatch
{
    private readonly ThemeSpec _theme;

    public ThemeSpecPatch(ThemeSpec theme)
    {
        _theme = theme;
    }

    public void Apply(UIScreen screen)
    {
        if (_theme == null || screen == null)
            return;

        Image rootImage = screen.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = _theme.backgroundColor;
        }

        TMP_Text[] texts = screen.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            text.color = _theme.textColor;
        }
    }
}

[CreateAssetMenu(menuName = "UI/ThemeSpec")]
public class ThemeSpec : ScriptableObject
{
    public Color backgroundColor = Color.white;
    public Color textColor = Color.black;

    // Fonts, button styles, etc. can be added later if necessary
    public void BuildPatches(System.Collections.Generic.List<IUIPatch> patches)
    {
        patches.Add(new ThemeSpecPatch(this));
    }
}