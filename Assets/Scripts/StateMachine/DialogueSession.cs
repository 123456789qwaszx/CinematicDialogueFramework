public sealed class DialogueSession
{
    private readonly DialogueResolver _resolver;
    private readonly DialogueGatePlanner _gatePlanner;
    private readonly DialogueGateRunner _gateRunner;
    private readonly NodeViewModelBuilder _vmBuilder;
    private readonly IDialogueNodeOutput _output;
    private readonly DialogueRouteCatalogSO _routeCatalog;

    private SituationSpec _situation;
    private DialogueRuntimeState _state;
    
    public DialogueContext Context { get; } = new DialogueContext
    {
        IsAutoMode = false,
        IsSkipping = false,
        TimeScale = 1f,
        AutoAdvanceDelay = 0.6f
    };

    public DialogueSession(
        DialogueResolver resolver,
        DialogueGatePlanner gatePlanner,
        DialogueGateRunner gateRunner,
        NodeViewModelBuilder vmBuilder,
        IDialogueNodeOutput output,
        DialogueRouteCatalogSO routeCatalog
        )
    {
        _resolver = resolver;
        _gatePlanner = gatePlanner;
        _gateRunner = gateRunner;
        _vmBuilder = vmBuilder;
        _output = output;
        _routeCatalog = routeCatalog;
    }
    

    private DialogueRuntimeState CreateInitialState(string routeKey)
    {
        DialogueRoute route = _routeCatalog.GetRoute(routeKey);
        return new DialogueRuntimeState
            { SituationKey = route.SituationKey, BranchKey = "Default", VariantKey = "Default", NodeCursor = 0, };
    }

    public void StartDialogue(string routeKey)
    {
        _situation = _resolver.Resolve(routeKey);
        _state = CreateInitialState(routeKey);

        _gatePlanner.BuildCurrentNodeGate(_situation, ref _state);
        EnterNode();
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

        while (!_state.IsNodeGateCompleted)
        {
            if (!_gateRunner.Tick(_state, Context))
                break;
        }

        if (_state.IsNodeGateCompleted)
        {
            _state.NodeCursor++;
            if (_state.NodeCursor >= _situation.nodes.Count)
            {
                _output.ShowSystemMessage("(End of Situation)");
                Stop();
                return;
            }

            _gatePlanner.BuildCurrentNodeGate(_situation, ref _state);
            EnterNode();
        }
    }

    private void EnterNode()
    {
        var vm = _vmBuilder.Build(_situation, _state);
        _output.Show(vm);

        var node = _situation.nodes[_state.NodeCursor];
        _output.Play(node, Context);
    }
}