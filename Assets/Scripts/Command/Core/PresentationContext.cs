using System;
using UnityEngine;

[Serializable]
public sealed class PresentationModes
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
public sealed class PresentationContext
{
    /// <summary>
    /// - Shared playback modes (Auto/Skip/TimeScale/Delay). Must be assigned.
    /// </summary>
    public PresentationModes Modes;

    // ---- per-session runtime flags ----
    public bool IsNodeBusy;    // Whether a presentation is currently playing (commands/typing/etc.) (session-local)
    public bool IsClosed;      // Whether this session has been closed (e.g., ESC / toast close)
    public bool BlockInput;    // Optionally blocks input for this session only (e.g., modal)

    // ---- convenience read-only views ----
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
    /// - On StartDialogue: reset only session-local flags (keep shared modes)
    /// </summary>
    public void ResetSessionFlagsForStart()
    {
        IsNodeBusy = false;
        IsClosed = false;
        BlockInput = false;
    }
}