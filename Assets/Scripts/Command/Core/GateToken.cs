using System;

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
/// An atomic token for step-gate progression.
/// - Represents only a "post-command progress condition".
/// - Evaluated after the current step's commands have been executed.
/// </summary>
[Serializable]
public struct GateToken
{
    public GateTokenType type;

    // For Delay
    public float seconds;

    // For Signal
    public string signalKey; // Ignore null/empty/whitespace-only values.

    public static GateToken Immediately() => new() { type = GateTokenType.Immediately };
    public static GateToken Input() => new() { type = GateTokenType.Input };
    public static GateToken Delay(float seconds) => new() { type = GateTokenType.Delay, seconds = seconds };
    public static GateToken Signal(string key) => new() { type = GateTokenType.Signal, signalKey = key };
}
