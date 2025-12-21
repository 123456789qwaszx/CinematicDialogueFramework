using System;
using System.Collections.Generic;
using UnityEngine;

public class UIScreen : MonoBehaviour
{
    private Dictionary<string, RectTransform> _slots;
    public RectTransform GetSlot(string name)
    {
        return _slots[name];
    }

    public void Build(UIBinder binder)
    {
        _slots = binder.BuildSlots(transform);
    }

    public void Open()  => gameObject.SetActive(true);
    public void Close() => Destroy(gameObject);
}

public class UIBinder
{
    private string[] SlotNames = { "Header", "Body", "Footer" };

    public Dictionary<string, RectTransform> BuildSlots(Transform root)
    {
        Dictionary<string, RectTransform> dict = new();
        foreach (String name in SlotNames)
            dict[name] = root.Find(name) as RectTransform;
        return dict;
    }
}
