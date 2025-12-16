using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SituationEntry
{
    public string situationId;
    public List<DialogueLine> lines = new();
}

[CreateAssetMenu(fileName="DialogueSequenceData", menuName="Dialogue/Sequence")]
public class DialogueSequenceData : ScriptableObject
{
    public string sequenceId;
    public List<SituationEntry> situations = new();
}