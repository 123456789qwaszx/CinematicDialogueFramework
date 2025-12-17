using System;
using System.Collections.Generic;

// 한 컷, 한 턴, 한 박자
[Serializable]
public class DialogueNodeSpec
{
    // 이 노드에서 실행 할 모든 연출 커맨드 모음
    public List<NodeCommandSpec> commands = new();

    // 이 노드가 끝난 뒤, 언제 다음 노드로 넘어갈지 결정하는 게이트 토큰 목록
    public List<GateToken> gateTokens = new() { GateToken.Input() };
}