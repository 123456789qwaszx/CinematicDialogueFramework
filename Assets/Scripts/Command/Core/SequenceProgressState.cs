using System;

[Serializable]
public sealed class SequenceProgressState
{
    public string RouteKey;
    public string StartKey;
    public int CurrentNodeIndex;
    
    public StepGateState StepGate;

    public SequenceProgressState(Route route)
    {
        RouteKey = route.RouteKey;
        StartKey = route.StartKey;
        CurrentNodeIndex = 0;
        StepGate = default;
    }
    
    public int StepGateTokenCount => StepGate.Count;
    public bool IsNodeStepsCompleted => StepGate.IsCompleted;
}