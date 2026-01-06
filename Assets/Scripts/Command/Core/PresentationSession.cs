using UnityEngine;

// PresentationSession is the sole owner of dialogue time progression.
// All other components may report state or perform execution,
// but only Tick() is allowed to advance steps or nodes.
public sealed class PresentationSession
{
    private readonly StepGatePlanBuilder _gatePlanner;
    private readonly StepGateAdvancer _gateAdvancer;
    private readonly CommandExecutor _executor;
    private readonly RouteCatalogSO _routeCatalog;
    
    public PresentationContext Context { get; }
    private readonly CommandRunScope _nodeScope;
    private SequenceSpecSO _sequence;
    
    // Runtime state
    private SequenceProgressState _state;
    
    public PresentationSession(
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

    public void Start(string routeKey)
    {
        if (!_routeCatalog.TryResolve(routeKey, out Route route, out SequenceSpecSO sequence))
        {
            Debug.LogWarning($"Session failed. routeKey='{routeKey}'");
            return;
        }
        
        _state = CreateInitialState(route);
        _sequence = sequence;

        _gatePlanner.BuildForCurrentNode(_sequence, _state);
        
        PresentAndPlayCurrentStep(
            nodeIndex: _state.CurrentNodeIndex,
            stepIndex: _state.StepGate.StepIndex);
    }

    public void Tick()
    {
        // === TIME PROGRESSION BEGINS ===

        if (_sequence == null || _state == null) return;

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

                if (_state.CurrentNodeIndex >= _sequence.nodes.Count)
                {
                    _gateAdvancer.ClearLatchedSignals();
                    EndDialogue();
                    return;
                }

                _gateAdvancer.ClearLatchedSignals();
                _gatePlanner.BuildForCurrentNode(_sequence, _state);

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
        
        _sequence = null;
        _state = null;
    }

    #endregion

    #region internal helpers

    private SequenceProgressState CreateInitialState(Route route)
    {
        return new SequenceProgressState
        {
            RouteKey = route.RouteKey,
            StartKey = route.StartKey,
            CurrentNodeIndex = 0,
            StepGate = default
        };
    }

    private void PresentAndPlayCurrentStep(int nodeIndex, int stepIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _sequence.nodes.Count)
            return;

        NodeSpec node = _sequence.nodes[nodeIndex];
        _executor.PlayStep(node, stepIndex, _nodeScope);
    }
    #endregion
}