using System;

/// <summary>
/// Evaluates the current step gate token and advances the step cursor when it is satisfied.
/// 
/// Contract:
/// - Returns true if it consumed at least one gate token (StepIndex advanced or Skip jumped to end).
/// - Returns false if blocked / waiting (e.g., busy animation, missing input, remaining delay, signal not yet received).
/// </summary>
public class StepGateAdvancer : IDisposable
{
    private readonly IInputSource _input;
    private readonly ITimeSource _time;
    private readonly ISignalBus _signals;

    // Signals are latched until consumed by a Signal gate token.
    private readonly System.Collections.Generic.HashSet<string> _latchedSignals = new();

    public StepGateAdvancer(IInputSource input, ITimeSource time, ISignalBus signals)
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
        if (!string.IsNullOrEmpty(key))
            _latchedSignals.Add(key);
    }

    /// <summary>
    /// Tries to consume the current step gate token.
    /// Returns true if one token was consumed (StepIndex advanced), otherwise false.
    /// </summary>
    public bool TryConsume(DialogueRuntimeState state, DialogueContext ctx)
    {
        if (state.StepGate.Tokens == null || state.StepGate.StepIndex >= state.StepGate.Tokens.Count)
            return false;

        // ✅ 0) 노드 연출이 아직 재생 중이면, 어떤 토큰도 소비하지 않는다 (Skip 모드 제외)
        if (ctx != null && ctx.IsNodeBusy && !ctx.IsSkipping)
            return false;

        // Skip: consume all remaining tokens immediately
        if (ctx != null && ctx.IsSkipping)
        {
            state.StepGate.StepIndex = state.StepGate.Tokens.Count;
            state.StepGate.InFlight = default;
            _latchedSignals.Clear();
            return true;
        }

        var tokenOpt = state.StepGate.CurrentToken;
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

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
            state.StepGate.InFlight.RemainingSeconds = delay;

        float dt = _time.UnscaledDeltaTime * (ctx.TimeScale <= 0f ? 0.01f : ctx.TimeScale);
        state.StepGate.InFlight.RemainingSeconds -= dt;

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
        {
            state.StepGate.InFlight.RemainingSeconds = 0f;
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

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
            state.StepGate.InFlight.RemainingSeconds = seconds;

        float dt = _time.UnscaledDeltaTime * (ctx != null && ctx.TimeScale > 0f ? ctx.TimeScale : 0.01f);
        state.StepGate.InFlight.RemainingSeconds -= dt;

        if (state.StepGate.InFlight.RemainingSeconds <= 0f)
        {
            state.StepGate.InFlight.RemainingSeconds = 0f;
            ConsumeCurrent(state);
            return true;
        }

        return false;
    }
    
    private bool TickSignal(DialogueRuntimeState state, string expectedKey)
    {
        state.StepGate.InFlight.WaitingSignalKey = expectedKey;
    
        if (!string.IsNullOrEmpty(expectedKey) && _latchedSignals.Remove(expectedKey))
        {
            state.StepGate.InFlight.WaitingSignalKey = null;
            ConsumeCurrent(state);
            return true;
        }
    
        return false;
    }
    
    public void ClearLatchedSignals()
    {
        _latchedSignals.Clear();
    }


    private static void ConsumeCurrent(DialogueRuntimeState state)
    {
        state.StepGate.StepIndex++;
        state.StepGate.InFlight = default;
    }
}
