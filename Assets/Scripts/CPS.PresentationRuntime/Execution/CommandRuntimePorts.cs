using System;

/// <summary>
/// Latches string-based signals and allows one-shot consumption.
/// </summary>
public interface ISignalLatch
{
    void Latch(string key); // Mark a signal as latched (idempotent).
    bool IsLatched(string key); // Returns true if the signal has been latched (non-consuming).
    bool Consume(string key);// Returns true only once per key; consumes the latched state.
    void Clear();
}

/// <summary>
/// One-shot "advance" input for the command system (click / key / tap).
/// Reading consumes the current press so it won't be processed twice.
/// </summary>
public interface IInputSource
{
    bool ConsumeAdvancePressed();
}

/// <summary>
/// Time source for gates/commands that use unscaled delta time.
/// </summary>
public interface ITimeSource
{
    float UnscaledDeltaTime { get; }
}

/// <summary>
/// Signal bus for commands (e.g., signal gates, RaiseSignal).
/// </summary>
public interface ISignalBus
{
    event Action<string> OnSignal;
    void Raise(string key);
}