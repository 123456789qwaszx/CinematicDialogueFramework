using System;

/// <summary>
/// Evaluates the current step-gate token and advances the step cursor when the token is satisfied.
///
/// Contract:
/// - Returns true if it consumed at least one gate token (StepIndex advanced, or Skip jumped to end).
/// - Returns false if blocked/waiting (e.g., node is busy, no input, delay not elapsed, signal not yet received).
/// </summary>
public class StepGateAdvancer : IDisposable
{
    private readonly IInputSource _input;
    private readonly ITimeSource _time;
    private readonly ISignalBus _signals;
    private readonly ISignalLatch _latch;

    public StepGateAdvancer(IInputSource input, ITimeSource time, ISignalBus signals, ISignalLatch latch)
    {
        _input = input;
        _time = time;
        _signals = signals;
        _latch = latch;
        _signals.OnSignal += OnSignal;
    }

    public void Dispose()
    {
        _signals.OnSignal -= OnSignal;
    }
    
    private void OnSignal(string key)
    {
        _latch.Latch(key);
    }

    /// <summary>
    /// Tries to advance the step gate:
    /// - On success: StepGate.StepIndex is advanced (or skipped to end).
    /// - On failure: blocked by input/time/signal/busy state.
    /// </summary>
    public bool TryAdvanceStepGate(SequenceProgressState state, PresentationSessionContext ctx)
    {
        if (state.StepGate.Tokens == null || state.StepGate.Cursor >= state.StepGate.Tokens.Count)
            return false;

        // If the node is still "busy" (typing/animations/commands running), do not consume any token.
        if (ctx.IsNodeBusy && !ctx.IsSkipping)
            return false;

        // Skip: jump the gate cursor to the end (skip all remaining tokens).
        if (ctx.IsSkipping)
        {
            state.StepGate.Cursor = state.StepGate.Tokens.Count;
            state.StepGate.InFlight = default;
            _latch.Clear();
            return true;
        }

        GateToken? tokenOpt = state.StepGate.CurrentToken;
        if (tokenOpt == null) return false;

        GateToken token = tokenOpt.Value;

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

    private bool TickInput(SequenceProgressState state, PresentationSessionContext ctx)
    {
        if (ctx.IsBlockingInput)
            return false;
        
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

    private bool TickAutoInput(SequenceProgressState state, PresentationSessionContext ctx)
    {
        float delay = ctx.AutoAdvanceDelay;

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
            state.StepGate.InFlight.RemainingSeconds = delay;
        
        float timeScale = ctx.TimeScale;
        
        float dt = _time.UnscaledDeltaTime * timeScale;
        state.StepGate.InFlight.RemainingSeconds -= dt;

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
        {
            state.StepGate.InFlight.RemainingSeconds = 0f;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }

    private bool TickDelay(SequenceProgressState state, PresentationSessionContext ctx, float seconds)
    {
        if (seconds <= 0f)
        {
            ConsumeCurrent(state);
            return true;
        }

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
            state.StepGate.InFlight.RemainingSeconds = seconds;
        
        float timeScale = ctx.TimeScale;

        float dt = _time.UnscaledDeltaTime * timeScale;
        state.StepGate.InFlight.RemainingSeconds -= dt;

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
        {
            state.StepGate.InFlight.RemainingSeconds = 0f;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }

    private bool TickSignal(SequenceProgressState state, string expectedKey)
    {
        state.StepGate.InFlight.WaitingSignalKey = expectedKey;

        if (!string.IsNullOrEmpty(expectedKey) && _latch.Consume(expectedKey))
        {
            state.StepGate.InFlight.WaitingSignalKey = null;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }

    public void ClearLatchedSignals()
    {
        _latch.Clear();
    }

    private static void ConsumeCurrent(SequenceProgressState state)
    {
        state.StepGate.Cursor++;
        state.StepGate.InFlight = default;
    }
}