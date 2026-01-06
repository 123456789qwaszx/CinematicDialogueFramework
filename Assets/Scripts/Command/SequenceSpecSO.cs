using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    /// Key used to locate this situation inside a DialogueSequenceData.
    /// Must match (DialogueRoute).SituationKey.
    /// </summary>
    public string sequenceKey;

    public List<NodeSpec> nodes = new();
}