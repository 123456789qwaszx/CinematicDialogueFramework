using System;
using UnityEngine.Serialization;

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
    public GateTokenType type;

    // For Delay
    public float seconds;

    // For Signal
    public string signalKey; // null/빈문자/공백(" ") 무시

    public static GateToken Immediately() => new() { type = GateTokenType.Immediately };
    public static GateToken Input() => new() { type = GateTokenType.Input };
    public static GateToken Delay(float seconds) => new() { type = GateTokenType.Delay, seconds = seconds };
    public static GateToken Signal(string key) => new() { type = GateTokenType.Signal, signalKey = key };
}
