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
