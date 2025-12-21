using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonWidget : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public void SetLabel(string labelText) => label.text = labelText;
    public void SetOnClick(Action action)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action?.Invoke());
    }
}
