using System.Collections.Generic;

public static class TimingGateTokenMapper
{
    /// <summary>
    /// TimingGateSpec(파이프라인) -> GateToken 리스트(상태머신) 변환
    /// - 대부분 1개 토큰으로 충분
    /// - WaitFlagInt는 현재 GateTokenType에 없으므로 "가상 Signal"로 매핑
    /// </summary>
    public static List<GateToken> ToTokens(TimingGateSpec gate)
    {
        var tokens = new List<GateToken>(1);

        switch (gate.type)
        {
            case TimingGateType.Immediate:
                tokens.Add(GateToken.Immediately());
                break;

            case TimingGateType.DelaySeconds:
                tokens.Add(GateToken.Delay(gate.delaySeconds));
                break;

            case TimingGateType.WaitInput:
                tokens.Add(GateToken.Input());
                break;

            case TimingGateType.WaitSignal:
                tokens.Add(GateToken.Signal(gate.signalId));
                break;

            case TimingGateType.WaitFlagInt:
                // GateTokenType에 FlagInt가 없으므로, 우선 Signal로 우회한다.
                // 예: Raise($"FlagInt:{flagKey}:{compareValue}") 를 게임 쪽에서 보내면 통과됨
                tokens.Add(GateToken.Signal($"FlagInt:{gate.flagKey}:{gate.compareValue}"));
                break;

            default:
                tokens.Add(GateToken.Immediately());
                break;
        }

        return tokens;
    }
}