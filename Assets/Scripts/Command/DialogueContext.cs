using System;
using UnityEngine;

[Serializable]
public sealed class DialoguePlaybackModes
{
    public bool IsAutoMode;
    public bool IsSkipping;
    public float TimeScale = 1f;
    public float AutoAdvanceDelay = 0.6f;

    public void ResetDefaults()
    {
        IsAutoMode = false;
        IsSkipping = false;
        TimeScale = 1f;
        AutoAdvanceDelay = 0.6f;
    }
}

[Serializable]
public sealed class DialogueContext
{
    [Tooltip("Shared playback modes (Auto/Skip/TimeScale/Delay). Must be assigned.")]
    public DialoguePlaybackModes Modes;

    // -------- per-session runtime flags --------
    public bool IsNodeBusy;    // 커맨드/타이핑 등 연출이 재생 중인지 (세션 로컬)
    public bool IsClosed;      // ESC/Toast 닫기 등으로 이 세션만 닫혔는지
    public bool BlockInput;    // 필요시 이 세션 입력만 막기(모달 등)

    // -------- convenience read-only views (shared) --------
    public bool IsAutoMode => Modes != null && Modes.IsAutoMode;
    public bool IsSkipping => Modes != null && Modes.IsSkipping;

    public float TimeScale
    {
        get
        {
            if (Modes == null) return 1f;
            return Modes.TimeScale <= 0f ? 0.01f : Modes.TimeScale;
        }
    }

    public float AutoAdvanceDelay => Modes != null ? Modes.AutoAdvanceDelay : 0.6f;

    /// <summary>
    /// 호출 정책 예시:
    /// - StartDialogue 때: 세션 로컬 플래그만 리셋(모드는 유지)
    /// </summary>
    public void ResetSessionFlagsForStart()
    {
        IsNodeBusy = false;
        IsClosed = false;
        BlockInput = false;
    }
}
