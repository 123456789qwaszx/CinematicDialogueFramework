using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueNodeSpec
{
    [Header("Commands in this Node (묶음)")]
    public List<NodeCommandSpec> commands = new();

    [Header("Progression Gate")]
    // 이 노드가 끝난 뒤, 언제 다음 노드로 넘어갈지 결정하는 게이트 토큰 목록
    // (없으면 Immediately 1개가 자동으로 들어간다고 보면 됨)
    public List<GateToken> gateTokens = new() { GateToken.Input() };
}