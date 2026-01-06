using System;
using System.Threading;

public class CommandRunScope
{
    public PresentationContext Playback { get; }
    public CancellationToken Token { get; set; }
    
    // Tracks things created during this step (disposed/cleaned up when the step ends)
    internal RunLifetime StepLifetime { get; } = new();
    
    // Added for command extensibility (e.g., BGM).
    // This lifetime can outlive a single step and is cleaned up when the run/session ends,
    internal RunLifetime RunLifetime  { get; } = new();
    
    public CommandRunScope(PresentationContext state)
    {
        Playback = state;
        Token = CancellationToken.None;
    }
    
    public bool IsSkipping => Playback != null && Playback.IsSkipping;
    public bool IsAutoMode => Playback != null && Playback.IsAutoMode;
    public float TimeScale => (Playback != null && Playback.TimeScale > 0f) ? Playback.TimeScale : 1f;
    public bool IsNodeBusy => Playback != null && Playback.IsNodeBusy;
    
    /// <summary>
    /// Must be called only by the Executor.
    /// </summary>
    public void SetNodeBusy(bool busy)
    {
        if (Playback != null)
            Playback.IsNodeBusy = busy;
    }
    
    public void CleanupStep(CleanupPolicy policy) => StepLifetime.Cleanup(policy); // Called when the step ends
    public void CleanupRun(CleanupPolicy policy) => RunLifetime.Cleanup(policy); // Called when the run/session ends

    // Domain-agnostic tracking (can register anything without depending on DOTween/Coroutine types)
    public void TrackStep(Action cancel, Action finish = null) => StepLifetime.Track(cancel, finish);
    public void TrackRun (Action cancel, Action finish = null) => RunLifetime.Track(cancel, finish);
}