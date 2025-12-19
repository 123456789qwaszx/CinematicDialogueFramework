using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueSequenceData", menuName = "Dialogue/Sequence")]
public class DialogueSequenceData : ScriptableObject
{
    public string sequenceId;

    // ✅ 인라인 작성 대신, 작성된 SituationSpecSO를 참조
    public List<SituationSpecSO> situations = new();

    private Dictionary<string, SituationSpecSO> _situationsDict;

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
        _situationsDict = new Dictionary<string, SituationSpecSO>(StringComparer.Ordinal);

        for (int i = 0; i < situations.Count; i++)
        {
            SituationSpecSO situation = situations[i];
            if (situation == null)
                continue;
            
            if (string.IsNullOrWhiteSpace(situation.situationKey))
                continue;
            
            string key = situation.situationKey;
            bool isNewKey = _situationsDict.TryAdd(key, situation);
            
            if (!isNewKey)
            {
                Debug.LogWarning($"Duplicate situationKey '{key}' in sequence '{name}'. Last one wins.");
                _situationsDict[key] = situation;
            }
        }
    }

    public bool TryGetSituation(string situationKey, out SituationSpecSO situation)
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