using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역 SituationKey -> (어떤 SequenceData의 어떤 Situation인지) 매핑.
/// 실행 모드는 이제 전부 StateMachine + Presenter 조합으로 통일.
/// </summary>
[Serializable]
public sealed class DialogueRouteEntry
{
    [Header("Global Key")]
    public string situationKey; // 외부에서 사용할 전역 키

    [Header("Data")]
    public DialogueSequenceData sequence;
    public string situationId; // sequence 내 SituationEntry.situationId
}

public readonly struct DialogueRoute
{
    public readonly string SituationKey;
    public readonly DialogueSequenceData Sequence;
    public readonly string SituationId;

    public string SequenceId => Sequence != null ? Sequence.sequenceId : null;

    public DialogueRoute(DialogueRouteEntry e)
    {
        SituationKey = e.situationKey;
        Sequence     = e.sequence;
        SituationId  = e.situationId;
    }
}

public interface IDialogueRouteCatalog
{
    bool TryGetRoute(string situationKey, out DialogueRoute route);
}

[CreateAssetMenu(fileName = "DialogueRouteCatalog", menuName = "Dialogue/Route Catalog")]
public sealed class DialogueRouteCatalogSO : ScriptableObject, IDialogueRouteCatalog
{
    [SerializeField] private List<DialogueRouteEntry> entries = new();

    private Dictionary<string, DialogueRouteEntry> _byKey;

    private void OnEnable()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Rebuild();
    }
#endif

    private void Rebuild()
    {
        _byKey = new Dictionary<string, DialogueRouteEntry>(StringComparer.Ordinal);

        foreach (var e in entries)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.situationKey)) continue;

            _byKey[e.situationKey] = e; // 마지막 값 우선
        }
    }

    public bool TryGetRoute(string situationKey, out DialogueRoute route)
    {
        route = default;

        if (_byKey == null) Rebuild();
        if (string.IsNullOrWhiteSpace(situationKey)) return false;

        if (_byKey.TryGetValue(situationKey, out var entry) && entry != null)
        {
            route = new DialogueRoute(entry);
            return true;
        }

        return false;
    }
}
