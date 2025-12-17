using System;

/// <summary>
/// Pure logic that interprets GateTokens and decides "when we can advance".
/// All rules for Auto / Skip / TimeScale / Delay / Signal are centralized here.
///
/// ✅ 중요한 원칙:
/// - 진행을 막는 대기 조건은 오직 여기(GateRunner + GateToken)에만 존재한다.
/// - View/Command 파이프라인은 오직 연출(타이핑/애니메이션)만 담당하고,
///   "아직 연출 중인지"를 DialogueContext.IsNodeBusy로만 알려준다.
/// </summary>
public class DialogueGateRunner : IDisposable
{
    private readonly IInputSource _input;
    private readonly ITimeSource _time;
    private readonly ISignalBus _signals;

    private string _lastSignalKey;

    public DialogueGateRunner(IInputSource input, ITimeSource time, ISignalBus signals)
    {
        _input   = input;
        _time    = time;
        _signals = signals;
        _signals.OnSignal += OnSignal;
    }

    public void Dispose()
    {
        _signals.OnSignal -= OnSignal;
    }

    
    private void OnSignal(string key)
    {
        // Actual key matching is handled during Tick
        _lastSignalKey = key;
    }

    /// <summary>
    /// Advances the current node's GateTokens as far as possible.
    /// - true: at least one token was consumed
    /// - false: cannot consume any more tokens (blocked / waiting)
    /// </summary>
    public bool Tick(DialogueRuntimeState state, DialogueContext ctx)
    {
        if (state.Gate.Tokens == null || state.Gate.TokenCursor >= state.Gate.Tokens.Count)
            return false;

        // ✅ 0) 노드 연출이 아직 재생 중이면, 어떤 토큰도 소비하지 않는다 (Skip 모드 제외)
        if (ctx != null && ctx.IsNodeBusy && !ctx.IsSkipping)
            return false;

        // Skip: consume all remaining tokens immediately
        if (ctx != null && ctx.IsSkipping)
        {
            state.Gate.TokenCursor = state.Gate.Tokens.Count;
            state.Gate.InFlight = default;
            return true;
        }

        var tokenOpt = state.Gate.CurrentToken;
        if (tokenOpt == null) return false;

        var token = tokenOpt.Value;

        switch (token.type)
        {
            case GateTokenType.Immediately:
                ConsumeCurrent(state);
                return true;

            case GateTokenType.Input:
                return TickInput(state, ctx);

            case GateTokenType.Delay:
                return TickDelay(state, ctx, token.seconds);

            case GateTokenType.Signal:
                return TickSignal(state, token.signalKey);

            default:
                return false;
        }
    }

    private bool TickInput(DialogueRuntimeState state, DialogueContext ctx)
    {
        if (ctx != null && ctx.IsAutoMode)
        {
            return TickAutoInput(state, ctx);
        }

        if (_input.ConsumeAdvancePressed())
        {
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }

    private bool TickAutoInput(DialogueRuntimeState state, DialogueContext ctx)
    {
        float delay = ctx.AutoAdvanceDelay <= 0f ? 0.4f : ctx.AutoAdvanceDelay;

        if (state.Gate.InFlight.RemainingSeconds <= 0f)
            state.Gate.InFlight.RemainingSeconds = delay;

        float dt = _time.UnscaledDeltaTime * (ctx.TimeScale <= 0f ? 0.01f : ctx.TimeScale);
        state.Gate.InFlight.RemainingSeconds -= dt;

        if (state.Gate.InFlight.RemainingSeconds <= 0f)
        {
            state.Gate.InFlight.RemainingSeconds = 0f;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }

    private bool TickDelay(DialogueRuntimeState state, DialogueContext ctx, float seconds)
    {
        if (seconds <= 0f)
        {
            ConsumeCurrent(state);
            return true;
        }

        if (state.Gate.InFlight.RemainingSeconds <= 0f)
            state.Gate.InFlight.RemainingSeconds = seconds;

        float dt = _time.UnscaledDeltaTime * (ctx != null && ctx.TimeScale > 0f ? ctx.TimeScale : 0.01f);
        state.Gate.InFlight.RemainingSeconds -= dt;

        if (state.Gate.InFlight.RemainingSeconds <= 0f)
        {
            state.Gate.InFlight.RemainingSeconds = 0f;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }


    private bool TickSignal(DialogueRuntimeState state, string expectedKey)
    {
        state.Gate.InFlight.WaitingSignalKey = expectedKey;

        if (_lastSignalKey == expectedKey)
        {
            _lastSignalKey = null;
            state.Gate.InFlight.WaitingSignalKey = null;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }

    private static void ConsumeCurrent(DialogueRuntimeState state)
    {
        state.Gate.TokenCursor++;
        state.Gate.InFlight = default;
    }
}
