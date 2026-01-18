#if UNITY_EDITOR
using System;
using UnityEngine;

[Serializable]
public sealed class StepClipboardBox : ScriptableObject
{
    [SerializeReference] public StepSpec step;
}
#endif