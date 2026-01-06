using System;

[Serializable]
public sealed class SequenceProgressState
{
    public string RouteKey;
    public string StartKey;
    public int CurrentNodeIndex;
    
    public StepGateState StepGate;
    
    public int StepGateTokenCount => StepGate.Count;
    public bool IsNodeStepsCompleted => StepGate.IsCompleted;
}