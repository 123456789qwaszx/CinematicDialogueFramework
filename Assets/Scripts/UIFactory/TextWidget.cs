using TMPro;
using UnityEngine;

public class TextWidget : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    public void SetText(string labelText) => label.text = labelText;
}
