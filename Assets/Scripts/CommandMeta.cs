using System;

public enum CommandTrackType
{
    Interaction = 0,
    Setup       = 10,
    Motion      = 20,
    Dialogue    = 30,
    FX          = 40,
}

public enum CommandPhase
{
    Setup = 0,
    Motion,
    Dialogue,
    FX,
    Teardown, // optional
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