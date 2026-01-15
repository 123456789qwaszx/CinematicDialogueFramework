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
    
    // ---- Session-owned context ----
    private readonly PresentationSessionContext _context;
    
    // ---- Active run (per-Session) ----
    private CommandRunScope _sessionScope;
    
    // ---- Runtime state ----
    private SequenceProgressState _state;
    private SequenceSpecSO _sequence;
    
    public bool IsRunning => _sequence != null && _state != null && _sessionScope != null;
    
    public PresentationSession(
        StepGatePlanBuilder gatePlanner,
        StepGateAdvancer gateAdvancer,
        CommandExecutor executor,
        PlaybackSettings modes
    )
    {
        _gatePlanner = gatePlanner;
        _gateAdvancer = gateAdvancer;
        _executor = executor;
        _context = new PresentationSessionContext( modes );
    }

    
    public void Start(Route route, SequenceSpecSO sequence)
    {
        if (sequence == null) return;
        
        _state = new SequenceProgressState(route);
        _sequence = sequence;
        
        _sessionScope?.CleanupRun(CleanupPolicy.Finish);
        _sessionScope?.CleanupStep(CleanupPolicy.Finish);
        //_sessionScope?.CancelAll();
        _sessionScope = new CommandRunScope(_context);

        _gatePlanner.BuildForCurrentNode(_sequence, _state);
        
        PlayStep(
            nodeIndex: _state.NodeIndex,
            stepIndex: _state.StepGate.Cursor);
    }

    public void Tick()
    {
        // === TIME PROGRESSION BEGINS ===
        if (_sequence == null || _state == null) return;
        
        if (_context == null || _context.CloseRequested)
        {
            End();
            return;
        }

        while (true)
        {
            bool advanced = _gateAdvancer.TryAdvanceStepGate(_state, _context);
            if (!advanced)
                break;
            
            if (_state.IsNodeCompleted)
            {
                // ---- Node boundary ----
                _state.NodeIndex++;
                int nextNodeIndex = _state.NodeIndex;

                if (_state.NodeIndex >= _sequence.nodes.Count)
                {
                    _gateAdvancer.ClearLatchedSignals();
                    End();
                    return;
                }

                _gateAdvancer.ClearLatchedSignals();
                _gatePlanner.BuildForCurrentNode(_sequence, _state);

                int firstStep = _state.StepGate.Cursor;
                PlayStep(nextNodeIndex, firstStep);
                return;
            }

            // ---- Step boundary ----
            int currentNodeIndex = _state.NodeIndex;
            int currentStep = _state.StepGate.Cursor;
            PlayStep(currentNodeIndex, currentStep);
        } 
        
        // === TIME PROGRESSION ENDS ===
    }
    
    public void RequestEnd()
    {
        _context.RequestClose();
    }
    
    
    private void PlayStep(int nodeIndex, int stepIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _sequence.nodes.Count)
            return;

        NodeSpec node = _sequence.nodes[nodeIndex];
        _executor.PlayStep(node, stepIndex, _sessionScope);
    }
    
    private void End()
    {
        _gateAdvancer.ClearLatchedSignals();
        _executor.FinishAll(); // clear the session scope.
        _sequence = null;
        _state = null;
    }
}