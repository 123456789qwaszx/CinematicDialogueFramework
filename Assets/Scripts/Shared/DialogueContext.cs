using System;

/// <summary>
/// Auto/Skip are not gate tokens but global execution modes, so they live in a Context.
/// This state is propagated across multiple systems (Runner/Typing/WaitInput, etc.).
/// </summary>
[Serializable]
public sealed class DialogueContext
{
    // 자동 진행 모드 여부 (ON이면 Input 게이트도 일정 딜레이 후 자동 통과)
    public bool IsAutoMode;

    // 스킵 모드 여부 (ON이면 가능한 모든 대기/연출을 즉시 통과하려고 시도)
    public bool IsSkipping;

    // 연출용 타임스케일 (0 이하로 가면 최소값으로 보정)
    public float TimeScale = 1f;

    // AutoMode일 때 Input 토큰 자동 진행 딜레이
    public float AutoAdvanceDelay = 0.6f;

    // ✅ 현재 노드의 연출(Command 파이프라인)이 아직 진행 중인지
    // - true이면 GateRunner는 어떤 GateToken도 소비하지 않는다 (Skip 모드 제외)
    public bool IsNodeBusy;
}
