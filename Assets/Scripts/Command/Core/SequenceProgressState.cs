using System;

[Serializable]
public sealed class SequenceProgressState
{
    public string RouteKey;
    public string StartKey;
    public int NodeIndex;
    
    public StepGateState StepGate;

    public SequenceProgressState(Route route)
    {
        RouteKey = route.RouteKey;
        StartKey = route.StartKey;
        NodeIndex = 0;
        StepGate = default;
    }
    
    public bool IsNodeCompleted => StepGate.IsCompleted;
}