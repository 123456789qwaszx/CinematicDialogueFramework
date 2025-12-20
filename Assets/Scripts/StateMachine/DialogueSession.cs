using UnityEngine;

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
    
    // Runtime state
    private SituationSpecSO _situation;
    private DialogueRuntimeState _state;
    private NodePlayScope _nodeScope;
    
    // Public surface
    public DialogueContext Context { get; } = new DialogueContext
    {
        IsAutoMode = false,
        IsSkipping = false,
        TimeScale = 1f,
        AutoAdvanceDelay = 0.6f
    };
    
    // Ctor
    public DialogueSession(
        DialogueResolver resolver,
        StepGatePlanBuilder gatePlanner,
        StepGateAdvancer gateRunner,
        NodeViewModelBuilder vmBuilder,
        IDialogueNodeOutput output,
        DialogueRouteCatalogSO routeCatalog,
        CommandService commandService
    )
    {
        _resolver = resolver;
        _gatePlanner = gatePlanner;
        _gateAdvancer = gateRunner;
        _vmBuilder = vmBuilder;
        _output = output;
        _routeCatalog = routeCatalog;
        _commandService = commandService;
    }
    
    
    #region Public API

    public void StartDialogue(string routeKey)
    {
        _nodeScope ??= new NodePlayScope(_commandService, Context);
        
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
        if (_situation == null || _state == null) return;

        while (true)
        {
            bool consumed = _gateAdvancer.TryConsume(_state, Context);
            if (!consumed)
                break;

            if (_state.IsNodeStepsCompleted )
            {
                _state.CurrentNodeIndex++;

                if (_state.CurrentNodeIndex >= _situation.nodes.Count)
                {
                    _gateAdvancer.ClearLatchedSignals();
                    _output.ShowSystemMessage("(End of Situation)");
                    Stop();
                    return;
                }
                
                _gateAdvancer.ClearLatchedSignals();
                _gatePlanner.BuildForCurrentNode(_situation, ref _state);

                // 다음 노드의 step 0 실행
                EnterStep();
                return;
            }

            // 아직 노드가 끝난 건 아니면 -> 다음 step 진입
            EnterStep();

            // EnterStep이 커맨드를 재생하면 Busy가 켜지고,
            // 다음 루프에서 GateRunner.Tick이 막히므로 자연스럽게 여기서 멈춘다.
        }
    }
    
    public void Stop()
    {
        _situation = null;
        _state = null;
        
        _output.Clear();
        _gateAdvancer.ClearLatchedSignals();
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

    private void EnterStep()
    {
        if (_situation == null || _state == null) return;
        if (_state.CurrentNodeIndex < 0 || _state.CurrentNodeIndex >= _situation.nodes.Count) return;

        Debug.Log($"[Gate] tokens={_state.StepGate.Tokens?.Count ?? -1}, cursor={_state.StepGate.StepIndex}");

        // ✅ stepIndex 기반 VM
        NodeViewModel viewModel = _vmBuilder.Build(_situation, _state);
        _output.Show(viewModel);

        DialogueNodeSpec node = _situation.nodes[_state.CurrentNodeIndex];

        int stepIndex = _state.StepGate.StepIndex;

        // ✅ 여기서 반드시 "현재 stepIndex"를 재생해야 Step1+가 이어진다
        _output.PlayStep(node, stepIndex, _nodeScope);
    }
    
    #endregion
}
