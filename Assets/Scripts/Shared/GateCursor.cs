using System;
using System.Collections.Generic;

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
