using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

/// <summary>
/// Runtime state that is meaningful only while the current gate token is in-flight.
/// </summary>
[Serializable]
public struct GateInFlight
{
    public float RemainingSeconds;   // While Delay is running
    public string WaitingSignalKey;  // While waiting for a Signal
}

/// <summary>
/// Runtime progress state for the step-gate token stream of the current node.
/// </summary>
[Serializable]
public struct StepGateState
{
    public List<GateToken> Tokens;     // Resolved gate tokens for all steps in the current node
    public int StepIndex;              // current step cursor (0...Count)
    public GateInFlight InFlight;      // Runtime in-flight values needed while the current token is active (e.g., Delay/Signal)
    
    public GateToken? CurrentToken
    {
        get
        {
            if (Tokens == null) return null;
            if (StepIndex < 0 || StepIndex >= Tokens.Count) return null;
            return Tokens[StepIndex];
        }
    }
    
    public int Count => Tokens?.Count ?? 0;
    
    public bool IsCompleted => StepIndex >= Count;
}
