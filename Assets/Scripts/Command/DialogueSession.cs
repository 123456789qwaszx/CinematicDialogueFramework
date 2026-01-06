using UnityEngine;

// DialogueSession is the sole owner of dialogue time progression.
// All other components may report state or perform execution,
// but only Tick() is allowed to advance steps or nodes.
public sealed class DialogueSession
{
    private readonly StepGatePlanBuilder _gatePlanner;
    private readonly StepGateAdvancer _gateAdvancer;
    private readonly CommandExecutor _executor;
    private readonly RouteCatalogSO _routeCatalog;
    
    public PresentationContext Context { get; }
    private readonly CommandRunScope _nodeScope;

    // Runtime state
    private SequenceSpecSO _situation;
    private DialogueRuntimeState _state;
    
    public DialogueSession(
        StepGatePlanBuilder gatePlanner,
        StepGateAdvancer gateRunner,
        CommandExecutor executor,
        RouteCatalogSO routeCatalog,
        PresentationModes modes
    )
    {
        _gatePlanner = gatePlanner;
        _gateAdvancer = gateRunner;
        _executor = executor;
        _routeCatalog = routeCatalog;
        Context = new PresentationContext { Modes = modes };
        _nodeScope = new CommandRunScope(Context);
    }
    

    #region Public API

    public void StartDialogue(string routeKey)
    {
        if (!_routeCatalog.TryResolve(routeKey, out Route route, out SequenceSpecSO sequence))
        {
            Debug.LogWarning($"StartDialogue failed. routeKey='{routeKey}'");
            return;
        }
        
        _state = CreateInitialState(route);
        _situation = sequence;

        _gatePlanner.BuildForCurrentNode(_situation, _state);
        
        PresentAndPlayCurrentStep(
            nodeIndex: _state.CurrentNodeIndex,
            stepIndex: _state.StepGate.StepIndex);
    }

    public void Tick()
    {
        // === TIME PROGRESSION BEGINS ===

        if (_situation == null || _state == null) return;

        while (true)
        {
            bool advanced = _gateAdvancer.TryAdvanceStepGate(_state, Context);
            if (!advanced)
                break;

            int currentNode = _state.CurrentNodeIndex;
            int currentStep = _state.StepGate.StepIndex;

            if (_state.IsNodeStepsCompleted)
            {
                // ---- Node boundary ----
                _state.CurrentNodeIndex++;
                int newNodeIndex = _state.CurrentNodeIndex;

                if (_state.CurrentNodeIndex >= _situation.nodes.Count)
                {
                    _gateAdvancer.ClearLatchedSignals();
                    EndDialogue();
                    return;
                }

                _gateAdvancer.ClearLatchedSignals();
                _gatePlanner.BuildForCurrentNode(_situation, _state);

                int firstStep = _state.StepGate.StepIndex;
                PresentAndPlayCurrentStep(newNodeIndex, firstStep);
                return;
            }

            // ---- Step boundary ----
            PresentAndPlayCurrentStep(currentNode, currentStep);
        }

        // === TIME PROGRESSION ENDS ===
    }

    public void EndDialogue()
    {
        _gateAdvancer.ClearLatchedSignals();
        
        _executor.FinishStep();
        
        _situation = null;
        _state = null;
    }

    #endregion

    #region internal helpers

    private DialogueRuntimeState CreateInitialState(Route route)
    {
        return new DialogueRuntimeState
        {
            RouteKey = route.RouteKey,
            SituationKey = route.StartKey,
            BranchKey = "Default",
            VariantKey = "Default",
            CurrentNodeIndex = 0,
            StepGate = default
        };
    }

    private void PresentAndPlayCurrentStep(int nodeIndex, int stepIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _situation.nodes.Count)
            return;

        NodeSpec node = _situation.nodes[nodeIndex];
        _executor.PlayStep(node, stepIndex, _nodeScope);
    }
    #endregion
}