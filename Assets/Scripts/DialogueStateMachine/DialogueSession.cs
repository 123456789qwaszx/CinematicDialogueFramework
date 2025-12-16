using System;

public class DialogueSession
{
    public event Action<NodeViewModel> OnNodeEntered;
    public event Action<int, int> OnTokenProgress; // (tokenCursor, tokenCount)
    public event Action OnSituationEnded;

    public DialogueRuntimeState State => _state;
    public DialogueContext Context;

    private readonly DialogueResolver _resolver;
    private readonly DialogueGateRunner _gateRunner;
    private readonly IDialoguePresenter _presenter;

    private SituationEntry _situation;
    private DialogueRuntimeState _state;

    public DialogueSession(DialogueResolver resolver, DialogueGateRunner gateRunner, IDialoguePresenter presenter)
    {
        _resolver   = resolver;
        _gateRunner = gateRunner;
        _presenter  = presenter;

        Context = new DialogueContext
        {
            IsAutoMode       = false,
            IsSkipping       = false,
            TimeScale        = 1f,
            AutoAdvanceDelay = 0.6f
        };
    }

    public bool Start(string situationKey)
    {
        var (situation, state) = _resolver.ResolveNew(situationKey);
        if (situation == null || state == null) return false;

        _situation = situation;
        _state     = state;

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

        // 현재 노드의 GateTokens 처리
        while (!_state.IsNodeGateCompleted)
        {
            bool moved = _gateRunner.Tick(_state, Context);
            if (!moved) break;

            progressed = true;
            OnTokenProgress?.Invoke(_state.Gate.TokenCursor, _state.CurrentNodeTokenCount);
        }

        // 노드 게이트 완료 → 다음 노드로 이동
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

            _resolver.ResolveCurrentNodeGate(_situation, _state);
            EnterNode();
            progressed = true;
        }

        // progressed는 디버깅용으로 필요하면 사용
    }

    private void EnterNode()
    {
        var vm = _resolver.BuildNodeViewModel(_situation, _state);
        _presenter.Present(vm);
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
        var (situation, state) = _resolver.ResolveNew(save.SituationKey);
        if (situation == null || state == null) return false;

        _situation = situation;
        _state     = state;

        _state.BranchKey  = save.BranchKey;
        _state.VariantKey = save.VariantKey;
        _state.NodeCursor = save.NodeCursor;

        _resolver.ResolveCurrentNodeGate(_situation, _state);

        _state.Gate.TokenCursor                 = save.TokenCursor;
        _state.Gate.InFlight.RemainingSeconds   = save.RemainingSeconds;
        _state.Gate.InFlight.WaitingSignalKey   = save.WaitingSignalKey;

        EnterNode();
        return true;
    }
}
