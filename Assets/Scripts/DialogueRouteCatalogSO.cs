using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueRouteKind
{
    StateMachine, // 상태머신 DialogueSession + GateRunner로 돌릴 때
    Pipeline      // 기존 DialogueManager 파이프라인으로 돌릴 때
}

[Serializable]
public sealed class DialogueRouteEntry
{
    [Header("Global Key")]
    public string situationKey; // 외부에서 사용할 전역 키

    [Header("Execution Mode")]
    public DialogueRouteKind kind = DialogueRouteKind.StateMachine;

    [Header("Shared Data (StateMachine / Pipeline 공통)")]
    public DialogueSequenceData sequence;
    public string situationId; // sequence 내 SituationEntry.situationId

    [Header("Pipeline 전용 옵션")]
    public TimingPlanSO timingPlanOverride; // null이면 DialogueManager.defaultTimingPlan 사용
}

public readonly struct DialogueRoute
{
    public readonly string SituationKey;
    public readonly DialogueRouteKind Kind;

    public readonly DialogueSequenceData Sequence;
    public readonly string SituationId;
    public readonly TimingPlanSO TimingPlanOverride;

    public string SequenceId => Sequence != null ? Sequence.sequenceId : null;

    public DialogueRoute(DialogueRouteEntry e)
    {
        SituationKey = e.situationKey;
        Kind         = e.kind;
        Sequence     = e.sequence;
        SituationId  = e.situationId;
        TimingPlanOverride = e.timingPlanOverride;
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
