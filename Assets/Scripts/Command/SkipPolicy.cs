public enum SkipPolicy
{
    /// <summary>스킵 중이면 이 커맨드는 실행하지 않는다(기본).</summary>
    Ignore = 0,

    /// <summary>스킵 중이면 즉시 완료 처리(OnSkip 호출).</summary>
    CompleteImmediately = 1,

    /// <summary>스킵 중이어도 그대로 실행한다(예: 중요한 시스템 연출/상태 반영).</summary>
    ExecuteEvenIfSkipping = 2
}