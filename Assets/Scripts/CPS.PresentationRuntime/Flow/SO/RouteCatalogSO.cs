using System;
using System.Collections.Generic;
using UnityEngine;

// Inspector-serialized definition (authoring data)
[Serializable]
public sealed class RouteMapping
{
    [Header("Entry point (Start input)")]
    [Tooltip("Lookup key passed to Start(routeKey). Must be unique within this catalog.")]
    public string routeKey;
    
    [Header("Target")]
    [Tooltip("Catalog to search for the start sequence.")]
    public SequenceCatalogSO sequenceCatalog;

    [Tooltip("SequenceSpecSO.sequenceKey to start from.")]
    public string startKey;
}

[CreateAssetMenu(fileName = "RouteCatalog", menuName = "Presentation/Route Catalog")]
public sealed class RouteCatalogSO : ScriptableObject, IRouteCatalog<Route>
{
    [SerializeField] private List<RouteMapping> routes = new();

    [Header("Fallback")]
    [SerializeField] private string fallbackRouteKey = "Default";
    [SerializeField] private string fallbackStartKey = "Default";

    private Dictionary<string, RouteMapping> _dict;

    private void OnEnable() => Rebuild();

#if UNITY_EDITOR
    private void OnValidate() => Rebuild();
#endif

    private void Rebuild()
    {
        _dict = new Dictionary<string, RouteMapping>(StringComparer.Ordinal);

        foreach (RouteMapping def in routes)
        {
            if (def == null) continue;
            if (string.IsNullOrWhiteSpace(def.routeKey)) continue;

            _dict[def.routeKey] = def;
        }
    }

    public bool TryGetRoute(string routeKey, out Route route)
    {
        route = default;

        if (_dict == null) Rebuild();

        if (!string.IsNullOrWhiteSpace(routeKey) &&
            _dict.TryGetValue(routeKey, out RouteMapping def) &&
            def != null)
        {
            route = new Route(def);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fallbackRouteKey) && 
            _dict.TryGetValue(fallbackRouteKey, out def) &&
            def != null)
        {
            route = new Route(def);
            return true;
        }

        Debug.LogWarning($"Route not found. routeKey='{routeKey}', fallbackRouteKey='{fallbackRouteKey}'");
        return false;
    }

    /// <summary>
    /// Resolve route + starting spec in one call.
    /// </summary>
    public bool TryResolve(string routeKey, out Route route, out SequenceSpecSO startSpec)
    {
        startSpec = null;

        if (!TryGetRoute(routeKey, out route))
            return false;

        if (route.SequenceCatalog == null)
        {
            Debug.LogWarning($"Route catalog is null. routeKey='{route.RouteKey}'");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(route.StartKey) &&
            route.SequenceCatalog.TryGetSequence(route.StartKey, out startSpec) &&
            startSpec != null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fallbackStartKey) &&
            route.SequenceCatalog.TryGetSequence(fallbackStartKey, out startSpec) &&
            startSpec != null)
        {
            return true;
        }

        Debug.LogWarning(
            $"Start spec not found. routeKey='{route.RouteKey}', startKey='{route.StartKey}', fallbackStartKey='{fallbackStartKey}'");
        return false;
    }
}
