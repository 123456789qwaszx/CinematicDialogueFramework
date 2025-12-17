using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SituationSpec
{
    /// <summary>
    /// Key used to locate this situation inside a DialogueSequenceData.
    /// Must match (DialogueRoute).SituationKey.
    /// </summary>
    public string situationKey;
    public List<DialogueNodeSpec> nodes = new();
}

[CreateAssetMenu(fileName = "DialogueSequenceData", menuName = "Dialogue/Sequence")]
public class DialogueSequenceData : ScriptableObject
{
    public string sequenceId;
    public List<SituationSpec> situations = new();
    
    private Dictionary<string, SituationSpec> _situationsDict;
    
    private void OnEnable()
    {
        RebuildIndex();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildIndex();
    }
#endif

    private void RebuildIndex()
    {
        _situationsDict = new Dictionary<string, SituationSpec>(StringComparer.Ordinal);

        for (int i = 0; i < situations.Count; i++)
        {
            SituationSpec situation = situations[i];
            _situationsDict[situation.situationKey] = situation;
        }
    }
    
    public bool TryGetSituation(string situationKey, out SituationSpec situation)
    {
        situation = null;

        if (string.IsNullOrWhiteSpace(situationKey))
        {
            Debug.LogWarning($"'{situationKey}': invalid input. situationKey is null/empty/whitespace.");
            return false;
        }

        if (_situationsDict == null)
            RebuildIndex();

        if (!_situationsDict.TryGetValue(situationKey, out situation))
        {
            Debug.LogWarning($"situationKey not found: '{situationKey}'");
            return false;
        }

        return true;
    }
}