using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SituationSpec", menuName = "Dialogue/Situation Spec")]
public class SituationSpecSO : ScriptableObject
{
    /// <summary>
    /// Key used to locate this situation inside a DialogueSequenceData.
    /// Must match (DialogueRoute).SituationKey.
    /// </summary>
    public string situationKey;

    public List<DialogueNodeSpec> nodes = new();
}