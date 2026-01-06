using System;

/// <summary>
/// Output port for the dialogue system.
/// The core uses this interface to present the current dialogue node (or system messages)
/// </summary>
public interface IDialoguePresenter
{
    // Present a display-oriented snapshot of the current node to the UI.
    void Present(NodeViewModel vm);
    
    // Present a non-dialogue system message (e.g., warnings, debug info, or status text).
    void PresentSystemMessage(string message);
    
    void Hide();
}

public interface INodeExecutor
{
    void PlayStep(NodeSpec node, int stepIndex, CommandRunScope scope, DialogueLine fallbackLine = null);
    void Stop();
    void FinishStep();
}

public interface ISignalLatch
{
    void Latch(string key);
    bool IsLatched(string key);
    bool Consume(string key);
    void Clear();
}

/// <summary>
/// Input port for "advance" actions (e.g., click / key press / tap).
/// The core queries this each frame and consumes the input when read,
/// so the same press is not processed multiple times.
/// </summary>
public interface IInputSource
{
    // Returns true if an "advance" input occurred during the current frame.
    // The input is consumed when read (one-shot).
    bool ConsumeAdvancePressed();
}

/// <summary>
/// Time port used by the core to drive time-based gates (e.g., Delay tokens).
/// Exposes unscaled delta time so dialogue progression remains consistent
/// even when the game's timeScale is modified.
/// </summary>
public interface ITimeSource
{
    float UnscaledDeltaTime { get; }
}

/// <summary>
/// Signal/event port for the dialogue system.
/// Used to wait for or react to external in-game events (Signal gate tokens).
/// Implementations may wrap UnityEvents, C# events, message buses, or custom systems.
/// </summary>
public interface ISignalBus
{
    // Fired when a signal is raised. The string is the signal key.
    event Action<string> OnSignal;
    
    // Raise a signal with the given key.
    void Raise(string key);
}