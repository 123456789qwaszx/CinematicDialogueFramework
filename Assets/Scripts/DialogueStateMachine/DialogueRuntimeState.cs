using System;
using System.Collections.Generic;

/// <summary>
/// "resolved runtime snapshot"
/// - Does NOT reference Spec assets directly (TimingSO / CommandSpec).
/// - Holds the resolved Route/Variant plus the current Node and Gate progression state.
/// </summary>
[Serializable]
public sealed class DialogueRuntimeState
{
    // 1) Identity / Route (Resolver output)
    public string SituationKey; // Which Situation (=Screen) this state belongs to (required)
    public string BranchKey; // Which decision branch was taken (required or "" allowed)
    public string VariantKey; // Selected variant within the branch (required or "Default")

    // 2) Cursor (position within the Situation)
    public int NodeCursor; // lineIndex / nodeIndex (required)

    // 3) Gate (token stream that blocks progression at the current node + progress cursor)
    public GateCursor Gate;

    // ---- Derived / Debug-friendly (does not have to be persisted) ----
    public int CurrentNodeTokenCount => Gate.Tokens?.Count ?? 0;
    public bool IsNodeGateCompleted => Gate.Tokens != null && Gate.TokenCursor >= Gate.Tokens.Count;
    public bool IsAtEndOfSituation => false; // Optional: overall length/end can be managed by Resolver/Session
}

/// <summary>
/// Progress state for the gate-token stream of the "current node".
/// Tokens must contain at least one entry (use an Explicit Immediately token if there is no wait).
/// </summary>
[Serializable]
public struct GateCursor
{
    /// <summary>
    /// Tokens resolved for the "current node" (at least one).
    /// Keeps only the resolved plan, not a reference to the original TimingSO.
    /// </summary>
    public List<GateToken> Tokens;

    /// <summary>
    /// Token cursor for the current node (which token is being consumed).
    /// </summary>
    public int TokenCursor;

    /// <summary>
    /// Runtime in-flight values needed while the current token is active (e.g., Delay/Signal).
    /// </summary>
    public GateInFlight InFlight;

    public bool HasTokens => Tokens != null && Tokens.Count > 0;

    public GateToken? CurrentToken
    {
        get
        {
            if (Tokens == null) return null;
            if (TokenCursor < 0 || TokenCursor >= Tokens.Count) return null;
            return Tokens[TokenCursor];
        }
    }
}

public enum GateTokenType
{
    /// <summary>No wait. An explicit token used to represent "none".</summary>
    Immediately,

    /// <summary>Wait for user input.</summary>
    Input,

    /// <summary>Wait for time.</summary>
    Delay,

    /// <summary>Wait for an in-game event/signal.</summary>
    Signal
}

/// <summary>
/// An atomic token for gate progression.
/// - Does NOT represent actions (no command execution).
/// - Represents only a "progress condition".
/// </summary>
[Serializable]
public struct GateToken
{
    public GateTokenType Type;

    // For Delay
    public float Seconds;

    // For Signal
    public string SignalKey;

    public static GateToken Immediately() => new() { Type = GateTokenType.Immediately };
    public static GateToken Input() => new() { Type = GateTokenType.Input };
    public static GateToken Delay(float seconds) => new() { Type = GateTokenType.Delay, Seconds = seconds };
    public static GateToken Signal(string key) => new() { Type = GateTokenType.Signal, SignalKey = key };
}

/// <summary>
/// State that is meaningful only while a token is "in flight".
/// Example: RemainingSeconds for Delay, WaitingSignalKey for Signal, etc.
/// </summary>
[Serializable]
public struct GateInFlight
{
    public float RemainingSeconds; // While Delay is running
    public string WaitingSignalKey; // While waiting for a Signal
}

/// <summary>
/// Auto/Skip are not gate tokens but global execution modes, so they live in a Context.
/// This state is propagated across multiple systems (Runner/Typing/WaitInput, etc.).
/// </summary>
[Serializable]
public struct DialogueContext
{
    public bool IsAutoMode;
    public bool IsSkipping;
    public float TimeScale;
    public float AutoAdvanceDelay;
}