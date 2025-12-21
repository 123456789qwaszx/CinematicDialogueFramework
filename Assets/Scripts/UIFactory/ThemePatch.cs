using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ThemeId { Light, Dark }

public interface IUIPatch
{
    void Apply(UIScreen screen);
}

public class ThemePatch : IUIPatch
{
    private readonly ThemeId _theme;
    public ThemePatch(ThemeId theme)
    {
        _theme = theme;
    }
    
    public void Apply(UIScreen screen)
    {
        Image img = screen.GetComponent<Image>();
        if (img == null) return;
        
        img.color = _theme switch
        {
            ThemeId.Light => Color.white,
            ThemeId.Dark  => Color.black,
            _             => Color.white
        };
    }
}
