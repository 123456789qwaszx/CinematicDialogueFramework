using System;
using System.Collections.Generic;
using UnityEngine;

// Inspector-serialized entry (authoring data)
[Serializable]
public sealed class RouteEntry
{
    [Header("Route ID (authoring key)")]
    public string routeKey;

    [Header("Data")]
    public SequenceDatabaseSO sequenceDatabase;

    [Tooltip("Key to start from within the selected database (e.g. situationKey).")]
    public string startKey;
}

// Immutable runtime snapshot
public readonly struct Route
{
    public readonly string RouteKey;
    public readonly SequenceDatabaseSO Database;
    public readonly string StartKey;

    public Route(RouteEntry entry)
    {
        RouteKey = entry.routeKey;
        Database = entry.sequenceDatabase;
        StartKey = entry.startKey;
    }
}

[CreateAssetMenu(fileName = "RouteCatalog", menuName = "CPS/Route Catalog")]
public sealed class RouteCatalogSO : ScriptableObject, IRouteCatalog<Route>
{
    [SerializeField] private List<RouteEntry> entries = new();
    
    [Header("Fallback")]
    [SerializeField] private string fallbackRouteKey = "Default";
    [SerializeField] private string fallbackStartKey = "Default";

    private Dictionary<string, RouteEntry> _dict;
    
    private void OnEnable() => Rebuild();
#if UNITY_EDITOR
    private void OnValidate() => Rebuild();
#endif
    
    private void Rebuild()
    {
        _dict = new Dictionary<string, RouteEntry>(StringComparer.Ordinal);

        foreach (RouteEntry entry in entries)
        {
            if (entry == null) continue;
            if (string.IsNullOrWhiteSpace(entry.routeKey)) continue;

            _dict[entry.routeKey] = entry;
        }
    }
    
    public bool TryGetRoute(string routeKey, out Route route)
    {
        route = default;

        if (_dict == null) Rebuild();

        // 1) try requested
        if (!string.IsNullOrWhiteSpace(routeKey) &&
            _dict.TryGetValue(routeKey, out var entry) &&
            entry != null)
        {
            route = new Route(entry);
            return true;
        }

        // 2) fallback route
        if (!string.IsNullOrWhiteSpace(fallbackRouteKey) &&
            _dict.TryGetValue(fallbackRouteKey, out entry) &&
            entry != null)
        {
            route = new Route(entry);
            return true;
        }

        Debug.LogWarning($"Route not found. routeKey='{routeKey}', fallbackRouteKey='{fallbackRouteKey}'");
        return false;
    }
    
    /// <summary>
    /// Resolve route + starting spec in one call (replaces the old Resolver).
    /// </summary>
    public bool TryResolve(string routeKey, out Route route, out SequenceSpecSO startSpec)
    {
        startSpec = null;

        if (!TryGetRoute(routeKey, out route))
            return false;

        if (route.Database == null)
        {
            Debug.LogWarning($"Route database is null. routeKey='{route.RouteKey}'");
            return false;
        }

        // 1) try route.StartKey
        if (!string.IsNullOrWhiteSpace(route.StartKey) &&
            route.Database.TryGetSituation(route.StartKey, out startSpec) &&
            startSpec != null)
        {
            return true;
        }

        // 2) fallback start key
        if (!string.IsNullOrWhiteSpace(fallbackStartKey) &&
            route.Database.TryGetSituation(fallbackStartKey, out startSpec) &&
            startSpec != null)
        {
            return true;
        }

        Debug.LogWarning(
            $"Start spec not found. routeKey='{route.RouteKey}', startKey='{route.StartKey}', fallbackStartKey='{fallbackStartKey}'");
        return false;
    }
}
