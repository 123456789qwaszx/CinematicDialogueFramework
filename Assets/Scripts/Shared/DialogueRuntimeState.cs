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
    public string RouteKey;
    public string SituationKey; // Which Situation (=Screen) this state belongs to (required)
    public string BranchKey; // Which decision branch was taken (required or "" allowed)
    public string VariantKey; // Selected variant within the branch (required or "Default")

    // 2) Cursor (position within the Situation)
    public int NodeCursor; // lineIndex / nodeIndex (required)

    // 3) Gate (token stream that blocks progression at the current node + progress cursor)
    public StepGateState Gate;

    // ---- Derived / Debug-friendly (does not have to be persisted) ----
    public int CurrentNodeTokenCount => Gate.StepGates?.Count ?? 0;
    public bool IsNodeGateCompleted => Gate.StepGates != null && Gate.StepIndex >= Gate.StepGates.Count;
    public bool IsAtEndOfSituation => false; // Optional: overall length/end can be managed by Resolver/Session
}