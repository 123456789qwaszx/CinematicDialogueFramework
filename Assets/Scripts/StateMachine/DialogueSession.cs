using UnityEngine;

// DialogueSession is the sole owner of dialogue time progression.
// All other components may report state or perform execution,
// but only Tick() is allowed to advance steps or nodes.
public sealed class DialogueSession
{
    private const string FallbackRouteKey = "Default";
    
    // Dependencies (injected)
    private readonly DialogueResolver _resolver;
    private readonly StepGatePlanBuilder _gatePlanner;
    private readonly StepGateAdvancer _gateAdvancer;
    private readonly NodeViewModelBuilder _vmBuilder;
    private readonly IDialogueNodeOutput _output;
    private readonly DialogueRouteCatalogSO _routeCatalog;
    
    private readonly CommandService _commandService;
    
    public DialogueContext Context { get; }
    private readonly NodePlayScope _nodeScope;

    // Runtime state
    private SituationSpecSO _situation;
    private DialogueRuntimeState _state;
    
    
    // Public surface
    
    // Ctor
    public DialogueSession(
        DialogueResolver resolver,
        StepGatePlanBuilder gatePlanner,
        StepGateAdvancer gateRunner,
        NodeViewModelBuilder vmBuilder,
        IDialogueNodeOutput output,
        DialogueRouteCatalogSO routeCatalog,
        CommandService commandService,
        DialoguePlaybackModes modes
    )
    {
        _resolver = resolver;
        _gatePlanner = gatePlanner;
        _gateAdvancer = gateRunner;
        _vmBuilder = vmBuilder;
        _output = output;
        _routeCatalog = routeCatalog;
        _commandService = commandService;
        Context = new DialogueContext { Modes = modes };
        _nodeScope = new NodePlayScope(commandService, Context);
    }
    
    
    //_nodeScope ??= new NodePlayScope(_commandService, Context);
    #region Public API

    public void StartDialogue(string routeKey)
    {
        
        _state = CreateInitialState(routeKey);
        if (_state == null)
        {
            Debug.LogWarning($"StartDialogue failed. Invalid routeKey='{routeKey}'");
            return;
        }

        _situation = _resolver.Resolve(routeKey);
        if (_situation == null)
        {
            Debug.LogWarning($"Missing SituationSpec. situationKey='{_state.SituationKey}', routeKey='{_state.RouteKey}'");
            return;
        }
        
        _gatePlanner.BuildForCurrentNode(_situation, ref _state);
    }

    public void Tick()
    {
        // === TIME PROGRESSION BEGINS ===
        
        if (_situation == null || _state == null) return;

        while (true)
        {
            bool consumed = _gateAdvancer.TryConsume(_state, Context);
            if (!consumed)
                break;

            if (_state.IsNodeStepsCompleted )
            {
                // ---- Node boundary ----
                _state.CurrentNodeIndex++;

                if (_state.CurrentNodeIndex >= _situation.nodes.Count)
                {
                    _gateAdvancer.ClearLatchedSignals();
                    _output.ShowSystemMessage("(End of Situation)");
                    EndDialogue();
                    return;
                }
                
                _gateAdvancer.ClearLatchedSignals();
                _gatePlanner.BuildForCurrentNode(_situation, ref _state);

                // 다음 노드의 step 0 실행
                PresentAndPlayCurrentStep();
                return;
            }

            // ---- Step boundary ----
            PresentAndPlayCurrentStep();
            
            // === TIME PROGRESSION ENDS ===
        }
    }
    
    public void EndDialogue()
    {
        _situation = null;
        _state = null;
        
        _gateAdvancer.ClearLatchedSignals();
        _output.Hide();
    }
    
    #endregion
    
    #region internal helpers
    
    private DialogueRuntimeState CreateInitialState(string routeKey)
    {
        if (!_routeCatalog.TryGetRoute(routeKey, out DialogueRoute route))
        {
            if (!_routeCatalog.TryGetRoute(FallbackRouteKey, out route))
                return null;
        }

        return new DialogueRuntimeState
        {
            RouteKey = routeKey,
            SituationKey = route.SituationKey,
            BranchKey = "Default",
            VariantKey = "Default",
            CurrentNodeIndex = 0,
            StepGate = default
        };
    }

    private void PresentAndPlayCurrentStep()
    {
        if (_situation == null || _state == null) return;
        if (_state.CurrentNodeIndex < 0 || _state.CurrentNodeIndex >= _situation.nodes.Count) return;

        //Debug.Log($"[Gate] tokens={_state.StepGate.Tokens?.Count ?? -1}, cursor={_state.StepGate.StepIndex}");

        
        NodeViewModel viewModel = _vmBuilder.Build(_situation, _state);
        _output.Show(viewModel);

        DialogueNodeSpec node = _situation.nodes[_state.CurrentNodeIndex];

        int stepIndex = _state.StepGate.StepIndex;

        // ✅ 여기서 반드시 "현재 stepIndex"를 재생해야 Step1+가 이어진다
        _output.PlayStep(node, stepIndex, _nodeScope);
    }
    
    #endregion
}
