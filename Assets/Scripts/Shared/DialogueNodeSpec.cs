using System.Collections.Generic;

[System.Serializable]
public class DialogueStepSpec
{
    public List<NodeCommandSpec> commands = new();
    public GateToken gate = default; // 이 Step이 끝난 뒤 다음 Step으로 넘어갈 Gate
}

[System.Serializable]
public class DialogueNodeSpec
{
    public List<DialogueStepSpec> steps = new();

    // (선택) 이전 구조 호환용 필드가 필요하면 HideInInspector로 유지 가능
    // [HideInInspector] public List<GateToken> gateTokens = new();
    // [HideInInspector] public List<NodeCommandSpec> commands = new();
}