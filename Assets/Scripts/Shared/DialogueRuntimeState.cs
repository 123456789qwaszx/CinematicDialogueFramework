using System;

[Serializable]
public sealed class DialogueRuntimeState
{
    public string RouteKey;
    public string SituationKey;
    public int CurrentNodeIndex;
    
    // Reserved (not used yet; kept for save compatibility)
    public string BranchKey;
    public string VariantKey;
    
    public StepGateState StepGate;
    
    public int StepGateTokenCount => StepGate.Count;
    public bool IsNodeStepsCompleted => StepGate.IsCompleted;
}