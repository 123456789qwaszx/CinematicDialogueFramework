using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Inspector-serialized entry (authoring data)
[Serializable]
public class DialogueRouteEntry
{
    [Header("Route ID (read-only at runtime)")]
    public string routeKey;

    [FormerlySerializedAs("sequenceData")] [Header("Data")]
    public SequenceDatabaseSO sequenceDatabase;
    [FormerlySerializedAs("se")] [FormerlySerializedAs("situationKey")] public string sequenceKey;
}

// Immutable runtime snapshot
public readonly struct DialogueRoute
{
    public readonly string RouteKey;
    public readonly SequenceDatabaseSO Sequence;
    /// <summary>
    /// Situation key to start from within the selected sequence.
    /// Must match (SituationSpec).situationKey.
    /// </summary>
    public readonly string SequenceKey;

    public DialogueRoute(DialogueRouteEntry entry)
    {
        RouteKey     = entry.routeKey;
        Sequence     = entry.sequenceDatabase;
        SequenceKey  = entry.sequenceKey;
    }
}

public interface IDialogueRouteCatalog
{
    bool TryGetRoute(string routeKey, out DialogueRoute route);
    DialogueRoute GetRoute(string routeKey);
}

[CreateAssetMenu(fileName = "DialogueRouteCatalog", menuName = "Dialogue/Route Catalog")]
public sealed class DialogueRouteCatalogSO : ScriptableObject, IDialogueRouteCatalog
{
    [SerializeField] private List<DialogueRouteEntry> entries = new();

    private Dictionary<string, DialogueRouteEntry> _routeEntriesDict;
    
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
        _routeEntriesDict = new Dictionary<string, DialogueRouteEntry>(StringComparer.Ordinal);

        foreach (DialogueRouteEntry entry in entries)
        {
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.routeKey)) continue;

            _routeEntriesDict[entry.routeKey] = entry;
        }
    }
    
    public bool TryGetRoute(string routeKey, out DialogueRoute route)
    {
        route = default;

        if (string.IsNullOrWhiteSpace(routeKey))
        {
            Debug.LogWarning($"'{routeKey}': invalid input. routeKey is null/empty/whitespace.");
            return false;
        }

        if (_routeEntriesDict == null)
            Rebuild();

        if (!_routeEntriesDict.TryGetValue(routeKey, out DialogueRouteEntry entry))
        {
            Debug.LogWarning($"routeKey not found: '{routeKey}'");
            return false;
        }

        route = new DialogueRoute(entry);
        return true;
    }

    public DialogueRoute GetRoute(string routeKey)
    {
        if (_routeEntriesDict == null)
            Rebuild();
        
        DialogueRouteEntry entry = _routeEntriesDict[routeKey];
        
        return new DialogueRoute(entry);
    }
}
