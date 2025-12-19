using System.Threading;

public class NodePlayScope
{
    public IPresentationPort Presenter { get; }
    private DialogueContext Playback { get; }
    public CancellationToken Token { get; set; }
    
    public NodePlayScope(IPresentationPort port, DialogueContext state)
    {
        Presenter = port;
        Playback = state;
        Token = CancellationToken.None;
    }
    
    
    public bool IsSkipping => Playback != null && Playback.IsSkipping;
    public bool IsAutoMode => Playback != null && Playback.IsAutoMode;
    public float TimeScale => (Playback != null && Playback.TimeScale > 0f) ? Playback.TimeScale : 1f;
    public bool IsNodeBusy => Playback != null && Playback.IsNodeBusy;

    /// <summary>
    /// this must be called only by the Executor
    /// </summary>
    public void SetNodeBusy(bool busy)
    {
        if (Playback != null)
            Playback.IsNodeBusy = busy;
    }
}