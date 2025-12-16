using System;
using System.Collections.Generic;
using UnityEngine;

public enum TimingGateType
{
    Immediate,       // 바로 진행
    DelaySeconds,    // n초 후
    WaitInput,       // 입력 대기(Next)
    WaitSignal,      // 외부 신호 대기
    WaitFlagInt,     // 플래그 조건(정수) 대기 - 필요하면 확장
}


[Serializable]
public struct TimingGateSpec
{
    public TimingGateType type;

    // DelaySeconds
    [Min(0f)] public float delaySeconds;

    // WaitSignal
    public string signalId;

    // WaitFlagInt
    public string flagKey;
    public int compareValue;
}

[Serializable]
public class TimingTokenSpec
{
    [Min(1)] public int consumeCount = 1;
    public TimingGateSpec gate;
    [TextArea] public string note;
}

[CreateAssetMenu(fileName = "TimingPlan", menuName = "Dialogue/Timing Plan", order = 0)]
public class TimingPlanSO : ScriptableObject
{
    [Header("Optional: 검증용 목표 커맨드 개수")]
    [Min(0)] public int expectedCommandCount;

    public List<TimingTokenSpec> tokens = new();

    public int TotalConsumeCount()
    {
        int sum = 0;
        foreach (var t in tokens)
            if (t != null) sum += Mathf.Max(0, t.consumeCount);
        return sum;
    }
}