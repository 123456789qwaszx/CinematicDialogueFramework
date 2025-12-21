using System.Collections.Generic;

public class UIPatchApplier
{
    public void Apply(UIScreen screen, List<IUIPatch> patches)
    {
        foreach (IUIPatch uiPatch in patches)
        {
            uiPatch.Apply(screen);
        }
    }
}