#if UNITY_EDITOR
using System;
using UnityEngine;

[Serializable]
internal sealed class CommandClipboardBox : ScriptableObject
{
    [SerializeReference] public CommandSpecBase spec;
}
#endif