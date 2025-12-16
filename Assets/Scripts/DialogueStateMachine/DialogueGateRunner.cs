/// <summary>
/// Pure logic that interprets GateTokens and decides "when we can advance".
/// All rules for Auto / Skip / TimeScale / Delay / Signal are centralized here.
/// </summary>
public class DialogueGateRunner
{
    private readonly IInputSource _input;
    private readonly ITimeSource _time;
    private readonly ISignalBus _signals;

    private bool _signalMatched;

    public DialogueGateRunner(IInputSource input, ITimeSource time, ISignalBus signals)
    {
        _input = input;
        _time = time;
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
        _signalMatched = true;
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

        // Skip: consume all remaining tokens immediately
        if (ctx.IsSkipping)
        {
            state.Gate.TokenCursor = state.Gate.Tokens.Count;
            state.Gate.InFlight = default;
            return true;
        }

        var tokenOpt = state.Gate.CurrentToken;
        if (tokenOpt == null) return false;

        var token = tokenOpt.Value;

        switch (token.Type)
        {
            case GateTokenType.Immediately:
                ConsumeCurrent(state);
                return true;

            case GateTokenType.Input:
                return TickInput(state, ctx);

            case GateTokenType.Delay:
                return TickDelay(state, ctx, token.Seconds);

            case GateTokenType.Signal:
                return TickSignal(state, token.SignalKey);

            default:
                return false;
        }
    }

    private bool TickInput(DialogueRuntimeState state, DialogueContext ctx)
    {
        if (ctx.IsAutoMode)
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

    private bool TickSignal(DialogueRuntimeState state, string key)
    {
        state.Gate.InFlight.WaitingSignalKey = key;

        if (_signalMatched)
        {
            _signalMatched = false;

            if (state.Gate.InFlight.WaitingSignalKey == key)
            {
                state.Gate.InFlight.WaitingSignalKey = null;
                ConsumeCurrent(state);
                return true;
            }
        }

        return false;
    }

    private static void ConsumeCurrent(DialogueRuntimeState state)
    {
        state.Gate.TokenCursor++;
        state.Gate.InFlight = default;
    }
}
