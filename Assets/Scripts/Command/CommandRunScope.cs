using System;
using System.Threading;

public class CommandRunScope
{
    public DialogueContext Playback { get; }
    public CancellationToken Token { get; set; }
    
    // 이번 run에서 만든 것들을 추적
    internal RunLifetime StepLifetime { get; } = new();
    
    // 커맨드(BGM 등)의 확장성 위해 추가. Step이 종료하더라도, 아예 Session이 끝나거나, 특정시점까지 유지.
    internal RunLifetime RunLifetime  { get; } = new();
    
    public CommandRunScope(DialogueContext state)
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
    
    // 스텝 종료 시 호출
    public void CleanupStep(CleanupPolicy policy) => StepLifetime.Cleanup(policy);
    // 세션 종료 시 호출
    public void CleanupRun(CleanupPolicy policy) => RunLifetime.Cleanup(policy);

    // 어떤 도메인이든 등록 가능 (DOTween/Coroutine 타입 몰라도 됨)
    public void TrackStep(Action cancel, Action finish = null) => StepLifetime.Track(cancel, finish);
    public void TrackRun (Action cancel, Action finish = null) => RunLifetime.Track(cancel, finish);
}