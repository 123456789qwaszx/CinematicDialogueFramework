// Immutable runtime snapshot
public readonly struct Route
{
    public readonly string RouteKey;
    public readonly SequenceCatalogSO SequenceCatalog;
    public readonly string StartKey;

    public Route(RouteMapping entry)
    {
        RouteKey = entry.routeKey;
        SequenceCatalog = entry.sequenceCatalog;
        StartKey = entry.startKey;
    }
}