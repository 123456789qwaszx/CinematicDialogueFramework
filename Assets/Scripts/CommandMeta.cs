using System;

public enum CommandTrackType
{
    /// <summary>
    /// Player/session interaction & flow control:
    /// input waits, signals, branching, route jumps, etc.
    /// </summary>
    Interaction = 0,
    
    /// <summary>Initial on/off/state setup for this step.</summary>
    Setup       = 10,
    
    /// <summary>Visual motion: slide, move, fade, shake...</summary>
    Motion      = 20,
    
    /// <summary>Dialogue text, speaker name, narration...</summary>
    Dialogue    = 30,
    
    /// <summary>Visual effects: flashes, overlays, particles...</summary>
    FX          = 40,
}

public enum CommandPhase
{
    /// <summary>
    /// Work that should happen immediately when this step starts,
    /// typically instant state setup.
    /// (enable/disable, position snaps, initial layout, etc.)
    /// </summary>
    Setup = 0,

    /// <summary>
    /// Main visible motion/tween phase.
    /// (SlideIn, MoveTo, Fade, Shake, etc.)
    /// </summary>
    Motion,

    /// <summary>
    /// The "center" timing of this line/beat.
    /// (TypeText, WaitForInput, Signal waits, auto-advance delays, etc.)
    /// </summary>
    Dialogue,

    /// <summary>
    /// FX layer that rides on top of the main flow.
    /// (screen flashes, speed lines, particles, overlays, etc.)
    /// </summary>
    FX,

    /// <summary>
    /// Cleanup phase right before/after this step ends.
    /// (turning off persistent effects, restoring state, final tidy-up)
    /// </summary>
    Teardown,
}

[Serializable]
public struct CommandMeta
{
    public CommandTrackType track;
    public CommandPhase phase;

    // Timing Preview용(계기판)
    public bool blockingHint;   // 기본적으로 step 진행을 잡는가?
    public bool infiniteHint;   // HoldSignal 같은 끝이 열린가?
    public float durationHint;  // 막대 길이(없으면 0)
}