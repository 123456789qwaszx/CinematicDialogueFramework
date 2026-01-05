using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SequenceSpec", menuName = "Presentation/SequenceSpec")]
public class SequenceSpecSO : ScriptableObject
{
    /// <summary>
    /// Key used to locate this situation inside a DialogueSequenceData.
    /// Must match (DialogueRoute).SituationKey.
    /// </summary>
    [FormerlySerializedAs("situationKey")] public string sequenceKey;

    public List<NodeSpec> nodes = new();
}