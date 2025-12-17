using System;

/// <summary>
/// Minimal state for save/load.
/// - Stores only what the Resolver can reconstruct (keys + cursors),
///   plus the minimal in-flight values required to restore an active gate token.
/// </summary>
[Serializable]
public struct DialogueSaveState
{
    public string RouteKey;
    public string SituationKey;
    public string BranchKey;
    public string VariantKey;

    public int NodeCursor;
    public int TokenCursor;

    // For restoring an in-flight token (Delay/Signal)
    public float RemainingSeconds;
    public string WaitingSignalKey;
}