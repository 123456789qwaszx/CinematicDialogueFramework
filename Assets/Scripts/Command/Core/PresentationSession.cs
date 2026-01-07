using UnityEngine;

// PresentationSession is the sole owner of dialogue time progression.
// All other components may report state or perform execution,
// but only Tick() is allowed to advance steps or nodes.
public sealed class PresentationSession
{
    // ---- Dependencies (injected) ----
    private readonly StepGatePlanBuilder _gatePlanner;
    private readonly StepGateAdvancer _gateAdvancer;
    private readonly CommandExecutor _executor;
    private readonly RouteCatalogSO _routeCatalog;
    
    // ---- Session-owned context ----
    public PresentationContext Context { get; }
    
    // ---- Active run (per-Session) ----
    private CommandRunScope _sessionScope;
    
    // ---- Runtime state ----
    private SequenceProgressState _state;
    private SequenceSpecSO _sequence;
    
    public bool IsRunning => _sequence != null && _state != null;
    
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
    }
    
    #region Public API

    public void Start(string routeKey)
    {
        if (!_routeCatalog.TryResolve(routeKey, out Route route, out SequenceSpecSO sequence))
        {
            Debug.LogWarning($"Session failed. routeKey='{routeKey}'");
            return;
        }
        
        _state = new SequenceProgressState(route);
        _sequence = sequence;
        _sessionScope = new CommandRunScope(Context);

        _gatePlanner.BuildForCurrentNode(_sequence, _state);
        
        PlayStep(
            nodeIndex: _state.CurrentNodeIndex,
            stepIndex: _state.StepGate.Cursor);
    }
    
    public void End()
    {
        _gateAdvancer.ClearLatchedSignals();
        _executor.FinishAll();
        _sequence = null;
        _state = null;
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
            
            if (_state.IsNodeStepsCompleted)
            {
                // ---- Node boundary ----
                _state.CurrentNodeIndex++;
                int newNodeIndex = _state.CurrentNodeIndex;

                if (_state.CurrentNodeIndex >= _sequence.nodes.Count)
                {
                    _gateAdvancer.ClearLatchedSignals();
                    End();
                    return;
                }

                _gateAdvancer.ClearLatchedSignals();
                _gatePlanner.BuildForCurrentNode(_sequence, _state);

                int firstStep = _state.StepGate.Cursor;
                PlayStep(newNodeIndex, firstStep);
                return;
            }

            // ---- Step boundary ----
            int currentNode = _state.CurrentNodeIndex;
            int currentStep = _state.StepGate.Cursor;
            PlayStep(currentNode, currentStep);
        } 
        
        // === TIME PROGRESSION ENDS ===
    }
    
    #endregion

    #region internal helpers

    private void PlayStep(int nodeIndex, int stepIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _sequence.nodes.Count)
            return;

        NodeSpec node = _sequence.nodes[nodeIndex];
        _executor.PlayStep(node, stepIndex, _sessionScope);
    }
    
    #endregion
}