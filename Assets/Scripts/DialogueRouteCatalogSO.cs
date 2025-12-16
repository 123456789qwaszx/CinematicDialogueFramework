using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueRouteKind
{
    StateMachine,
    Pipeline
}

[Serializable]
public sealed class DialogueRouteEntry
{
    [Header("Global Key")]
    public string situationKey;

    [Header("Route Kind")]
    public DialogueRouteKind kind = DialogueRouteKind.StateMachine;

    [Header("StateMachine")]
    public DialogueSituationSpec stateMachineSpec;

    [Header("Pipeline")]
    public DialogueSequenceData sequence;
    public string pipelineSituationId;

    [Header("Optional")]
    public TimingPlanSO timingPlanOverride; // null이면 DialogueManager의 defaultTimingPlan 사용
}

public readonly struct DialogueRoute
{
    public readonly string SituationKey;
    public readonly DialogueRouteKind Kind;

    public readonly DialogueSituationSpec StateMachineSpec;

    public readonly DialogueSequenceData Sequence;
    public readonly string PipelineSituationId;
    public readonly TimingPlanSO TimingPlanOverride;

    public string SequenceId => Sequence != null ? Sequence.sequenceId : null;

    public DialogueRoute(DialogueRouteEntry e)
    {
        SituationKey = e.situationKey;
        Kind = e.kind;

        StateMachineSpec = e.stateMachineSpec;

        Sequence = e.sequence;
        PipelineSituationId = e.pipelineSituationId;
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
