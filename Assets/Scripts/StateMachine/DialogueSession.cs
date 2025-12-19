using UnityEditor;
using UnityEngine;

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
    
    //---AddCustomCommand---
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
        DialogueGatePlanner gatePlanner,
        DialogueGateRunner gateRunner,
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
            {
                return null;
            }
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
            Debug.LogWarning($"StartDialogue failed. Invalid routeKey='{routeKey}");
            return;
        }
        _situation = _resolver.Resolve(routeKey);
        if (_situation == null)
        {
            Debug.LogWarning($"Missing SituationSpec. situationKey='{_state.SituationKey}', routeKey='{_state.RouteKey}'");
            return;
        }
        
        if (_nodeScope == null || !ReferenceEquals(_nodeScope.Playback, Context))
            _nodeScope = new NodePlayScope(_commandService, Context);

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
        Debug.Log($"[Gate] tokens={_state.Gate.Tokens?.Count ?? -1}, cursor={_state.Gate.TokenCursor}");

        NodeViewModel viewModel = _vmBuilder.Build(_situation, _state);
        _output.Show(viewModel);

        DialogueNodeSpec node = _situation.nodes[_state.NodeCursor];
        
        _output.Play(node, _nodeScope);
    }
}