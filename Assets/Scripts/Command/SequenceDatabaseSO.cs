using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SequenceDataBase", menuName = "Dialogue/Sequence")]
public class SequenceDatabaseSO : ScriptableObject
{
    public string sequenceId;

    public List<SequenceSpecSO> situations = new();

    private Dictionary<string, SequenceSpecSO> _situationsDict;

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
        _situationsDict = new Dictionary<string, SequenceSpecSO>(StringComparer.Ordinal);

        for (int i = 0; i < situations.Count; i++)
        {
            SequenceSpecSO situation = situations[i];
            if (situation == null)
                continue;
            
            if (string.IsNullOrWhiteSpace(situation.sequenceKey))
                continue;
            
            string key = situation.sequenceKey;
            bool isNewKey = _situationsDict.TryAdd(key, situation);
            
            if (!isNewKey)
            {
                Debug.LogWarning($"Duplicate situationKey '{key}' in sequence '{name}'. Last one wins.");
                _situationsDict[key] = situation;
            }
        }
    }

    public bool TryGetSituation(string situationKey, out SequenceSpecSO situation)
    {
        situation = null;

        if (string.IsNullOrWhiteSpace(situationKey))
        {
            Debug.LogWarning($"'{situationKey}': invalid input. situationKey is null/empty/whitespace.");
            return false;
        }

        if (_situationsDict == null)
            RebuildIndex();

        if (!_situationsDict.TryGetValue(situationKey, out situation) || situation == null)
        {
            Debug.LogWarning($"situationKey not found: '{situationKey}'");
            return false;
        }

        return true;
    }
}