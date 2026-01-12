using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoring-time catalog (ScriptableObject):
/// - Holds a list of SequenceSpecSO assets
/// - Builds a runtime index for fast lookup by sequenceKey
/// </summary>
[CreateAssetMenu(fileName = "SequenceCatalog", menuName = "Presentation/Sequence Catalog")]
public sealed class SequenceCatalogSO : ScriptableObject
{
    [Header("Optional metadata")]
    public string catalogId;

    [Header("Entries")]
    [SerializeField] private List<SequenceSpecSO> sequences = new();

    private Dictionary<string, SequenceSpecSO> _byKey;

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
        _byKey = new Dictionary<string, SequenceSpecSO>(StringComparer.Ordinal);

        if (sequences == null)
            return;

        for (int i = 0; i < sequences.Count; i++)
        {
            SequenceSpecSO spec = sequences[i];
            if (spec == null)
                continue;

            string key = spec.sequenceKey;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!_byKey.TryAdd(key, spec))
            {
                Debug.LogWarning($"Duplicate sequenceKey '{key}' in catalog '{name}'.");
                _byKey[key] = spec;
            }
        }
    }

    public bool TryGetSequence(string sequenceKey, out SequenceSpecSO sequence)
    {
        sequence = null;

        if (string.IsNullOrWhiteSpace(sequenceKey))
        {
            Debug.LogWarning($"Invalid input. sequenceKey is null/empty/whitespace. (catalog='{name}')");
            return false;
        }

        if (_byKey == null)
            RebuildIndex();

        if (!_byKey.TryGetValue(sequenceKey, out sequence) || sequence == null)
        {
            Debug.LogWarning($"sequenceKey not found: '{sequenceKey}' (catalog='{name}')");
            return false;
        }

        return true;
    }
}
