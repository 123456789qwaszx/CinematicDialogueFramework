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

[CreateAssetMenu(fileName = "SequenceSpec", menuName = "Presentation/SequenceSpec")]
public class SequenceSpecSO : ScriptableObject
{
    /// <summary>
    /// Key used to locate this sequence inside a SequenceCatalogSO.
    /// Must match (RouteCatalogSO).(RouteDefinition).StartKey(string).
    /// </summary>
    public string sequenceKey;

    public List<NodeSpec> nodes = new();
}