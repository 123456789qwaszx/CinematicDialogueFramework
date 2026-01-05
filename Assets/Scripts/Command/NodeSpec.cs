using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StepSpec
{
    public string editorName;
    [SerializeReference] public List<CommandSpecBase> commands = new();
    public GateToken gate;
}

[System.Serializable]
public class NodeSpec
{
    public string editorName;
    public List<StepSpec> steps = new();
}