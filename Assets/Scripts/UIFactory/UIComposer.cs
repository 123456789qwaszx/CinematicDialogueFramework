using UnityEngine;

public class UIComposer
{
    private readonly WidgetFactory _widgets;

    public UIComposer(WidgetFactory widgets)
    {
        _widgets = widgets;
    }

    public void Compose(UIScreen screen, UIScreenSpec spec, UIRouter router)
    {
        foreach (SlotSpec slotSpec in spec.slots)
        {
            string slotName = slotSpec.slotName;
            RectTransform slot = screen.GetSlot(slotName);

            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(slot.GetChild(i).gameObject);
            }

            foreach (WidgetSpec widgetSpec in slotSpec.widgets)
            {
                MonoBehaviour widget = _widgets.Create(widgetSpec, slot);
            }
        }
    }
}
