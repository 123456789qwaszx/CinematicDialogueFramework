using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StepSpec
{
    [SerializeReference]
    public List<CommandSpecBase> commands = new();
    
    public GateToken gate = default; // 이 Step이 끝난 뒤 다음 Step으로 넘어갈 Gate
}

[System.Serializable]
public class NodeSpec
{
    public List<StepSpec> steps = new();
}