using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueSession
{
    public event Action<NodeViewModel> OnNodeEntered;
    public event Action<int, int> OnTokenProgress;
    public event Action OnSituationEnded;

    public DialogueRuntimeState State => _state;
    public DialogueContext Context;

    private readonly DialogueResolver _resolver;
    private readonly DialogueGatePlanner _gatePlanner;
    private readonly DialogueGateRunner _gateRunner;
    private readonly IDialoguePresenter _presenter;
    private readonly NodeViewModelBuilder _nodeViewModelBuilder;

    private SituationSpec _situation;
    private DialogueRuntimeState _state;
    
    private readonly IDialogueRouteCatalog _routes;

    public DialogueSession(DialogueResolver resolver,
        DialogueGatePlanner gatePlanner,
        DialogueGateRunner gateRunner,
        IDialoguePresenter presenter,
        NodeViewModelBuilder nodeViewModelBuilder)
    {
        _resolver   = resolver;
        _gatePlanner = gatePlanner;
        _gateRunner = gateRunner;
        _presenter  = presenter;
        _nodeViewModelBuilder = nodeViewModelBuilder;

        Context = new DialogueContext
        {
            IsAutoMode       = false,
            IsSkipping       = false,
            TimeScale        = 1f,
            AutoAdvanceDelay = 0.6f
        };
    }
    
    private DialogueRuntimeState CreateInitialState(string routeKey)
    {
        DialogueRoute route = _routes.GetRoute(routeKey);
        
        return new DialogueRuntimeState
        {
            SituationKey = route.SituationKey,

            BranchKey    = "Default",
            VariantKey   = "Default",
            NodeCursor   = 0,
        };
    }

    public bool Start(string routeKey)
    {
        DialogueRuntimeState state = CreateInitialState(routeKey);
        SituationSpec situation = _resolver.Resolve(routeKey);
        
        _state = state;
        _situation = situation;
        
        _gatePlanner.BuildCurrentNodeGate(_situation, ref _state);

        EnterNode();
        return true;
    }

    public void Stop()
    {
        _situation = null;
        _state     = null;
        _presenter.Clear();
    }

    public void Tick()
    {
        if (_situation == null || _state == null) return;

        if (_situation.nodes == null || _situation.nodes.Count == 0)
        {
            _presenter.PresentSystemMessage("(Empty Situation)");
            OnSituationEnded?.Invoke();
            Stop();
            return;
        }

        if (_state.NodeCursor < 0 || _state.NodeCursor >= _situation.nodes.Count)
        {
            _presenter.PresentSystemMessage("(End of Situation)");
            OnSituationEnded?.Invoke();
            Stop();
            return;
        }

        bool progressed = false;

        while (!_state.IsNodeGateCompleted)
        {
            bool moved = _gateRunner.Tick(_state, Context);
            if (!moved) break;

            progressed = true;
            OnTokenProgress?.Invoke(_state.Gate.TokenCursor, _state.CurrentNodeTokenCount);
        }

        if (_state.IsNodeGateCompleted)
        {
            _state.NodeCursor++;

            if (_state.NodeCursor >= _situation.nodes.Count)
            {
                _presenter.PresentSystemMessage("(End of Situation)");
                OnSituationEnded?.Invoke();
                Stop();
                return;
            }

            _gatePlanner.BuildCurrentNodeGate(_situation, ref _state);
            EnterNode();
            progressed = true;
        }
    }

    private void EnterNode()
    {
        NodeViewModel vm =_nodeViewModelBuilder.Build(_situation, _state);
        _presenter.Present(vm);
        
        DialogueNodeSpec node = _situation.nodes[_state.NodeCursor];
        List<NodeCommandSpec> commands = node.commands; // CommandRunner가 실행
        
        OnNodeEntered?.Invoke(vm);
        OnTokenProgress?.Invoke(_state.Gate.TokenCursor, _state.CurrentNodeTokenCount);
    }

    public DialogueSaveState ExportSave()
    {
        return new DialogueSaveState
        {
            SituationKey     = _state.SituationKey,
            BranchKey        = _state.BranchKey,
            VariantKey       = _state.VariantKey,
            NodeCursor       = _state.NodeCursor,
            TokenCursor      = _state.Gate.TokenCursor,
            RemainingSeconds = _state.Gate.InFlight.RemainingSeconds,
            WaitingSignalKey = _state.Gate.InFlight.WaitingSignalKey
        };
    }

    public bool ImportSave(DialogueSaveState save)
    {
        DialogueRuntimeState state = CreateInitialState(save.RouteKey);
        SituationSpec situation = _resolver.Resolve(save.RouteKey);
        _gatePlanner.BuildCurrentNodeGate(_situation, ref _state);
        
        
        if (situation == null || state == null)
        {
            Debug.Log("Import SaveData Failed");
            return false;
        }

        _situation = situation;
        _state     = state;

        _state.BranchKey  = save.BranchKey;
        _state.VariantKey = save.VariantKey;
        _state.NodeCursor = save.NodeCursor;
        
        _gatePlanner.BuildCurrentNodeGate(_situation, ref _state);

        _state.Gate.TokenCursor               = save.TokenCursor;
        _state.Gate.InFlight.RemainingSeconds = save.RemainingSeconds;
        _state.Gate.InFlight.WaitingSignalKey = save.WaitingSignalKey;

        EnterNode();
        return true;
    }
}
