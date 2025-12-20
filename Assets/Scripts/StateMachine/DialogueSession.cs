using UnityEngine;

public sealed class DialogueSession
{
    private readonly DialogueResolver _resolver;
    private readonly StepGatePlanBuilder _gatePlanner;
    private readonly StepGateAdvancer _gateRunner;
    private readonly NodeViewModelBuilder _vmBuilder;
    private readonly IDialogueNodeOutput _output;
    private readonly DialogueRouteCatalogSO _routeCatalog;

    private SituationSpecSO _situation;
    private DialogueRuntimeState _state;

    private CommandService _commandService;
    private NodePlayScope _nodeScope;

    public DialogueContext Context { get; } = new DialogueContext
    {
        IsAutoMode = false,
        IsSkipping = false,
        TimeScale = 1f,
        AutoAdvanceDelay = 0.6f
    };

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
        _gateRunner = gateRunner;
        _vmBuilder = vmBuilder;
        _output = output;
        _routeCatalog = routeCatalog;
        _commandService = commandService;
    }

    private const string FallbackRouteKey = "Default";

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
            NodeCursor = 0,
            Gate = default
        };
    }

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
            // 여기서도 VM을 보여주고 싶다면 _output.ShowSystemMessage(...) 같은 걸로 처리 가능
            return;
        }

        if (_nodeScope == null || !ReferenceEquals(_nodeScope.Playback, Context))
            _nodeScope = new NodePlayScope(_commandService, Context);

        // 노드 게이트 계획(= steps.Count만큼 Tokens)
        _gatePlanner.BuildForCurrentNode(_situation, ref _state);

        // ✅ 노드 진입 시 step 0 실행
        EnterStep();
    }

    public void Stop()
    {
        _situation = null;
        _state = null;
        _output.Clear();
    }

    public void Tick()
    {
        if (_situation == null || _state == null) return;

        // ✅ 핵심: "토큰이 소비되는 순간"을 트리거로 다음 step/다음 노드로 진행
        // GateRunner 내부에서 Busy면 false를 반환하므로, 여기서 따로 Busy 체크 안 해도 됨.
        while (true)
        {
            bool consumed = _gateRunner.TryConsume(_state, Context);
            if (!consumed)
                break;

            // 소비 후 TokenCursor는 "다음 step"을 가리킴
            if (_state.IsNodeGateCompleted)
            {
                // 노드(step) 모두 끝 -> 다음 노드
                _state.NodeCursor++;

                if (_state.NodeCursor >= _situation.nodes.Count)
                {
                    _output.ShowSystemMessage("(End of Situation)");
                    Stop();
                    return;
                }

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

    private void EnterStep()
    {
        if (_situation == null || _state == null) return;
        if (_state.NodeCursor < 0 || _state.NodeCursor >= _situation.nodes.Count) return;

        Debug.Log($"[Gate] tokens={_state.Gate.StepGates?.Count ?? -1}, cursor={_state.Gate.StepIndex}");

        // ✅ stepIndex 기반 VM
        NodeViewModel viewModel = _vmBuilder.Build(_situation, _state);
        _output.Show(viewModel);

        DialogueNodeSpec node = _situation.nodes[_state.NodeCursor];

        int stepIndex = _state.Gate.StepIndex;

        // ✅ 여기서 반드시 "현재 stepIndex"를 재생해야 Step1+가 이어진다
        _output.PlayStep(node, stepIndex, _nodeScope);
    }
}
