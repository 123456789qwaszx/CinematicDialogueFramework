using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public abstract class CommandSpecBase
{
    // 이 커맨드가 속한 화면 (UI Screen) 식별자
    public string screenId;
    // 이 화면 안에서 "어느 역할/세트"와 계약하는지.
    public string roleKey;

    // ---- Baked meta (runtime reads this; editor writes this) ----
    [SerializeField, HideInInspector] private CommandMeta _meta;
    public CommandMeta Meta => _meta;

#if UNITY_EDITOR
    public void Editor_SetMeta(CommandMeta meta) => _meta = meta;
#endif
}