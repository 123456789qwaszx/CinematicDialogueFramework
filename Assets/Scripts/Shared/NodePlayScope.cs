using System;
using System.Threading;

public class NodePlayScope
{
    public DialogueContext Playback { get; }
    public CancellationToken Token { get; set; }
    
    // 이번 run에서 만든 것들을 추적
    internal RunLifetime Lifetime { get; } = new();
    
    public NodePlayScope(DialogueContext state)
    {
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
    
    // 어떤 도메인이든 등록 가능 (DOTween/Coroutine 타입 몰라도 됨)
    public void Track(Action cancel, Action finish = null)
        => Lifetime.Track(cancel, finish);

    // Executor가 Stop/Skip/종료 시 호출하는 단일 정리 포인트
    public void Cleanup(CleanupPolicy policy)
    {
        Lifetime.Cleanup(policy);
    }
}