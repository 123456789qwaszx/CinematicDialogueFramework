public interface IRouteCatalog<TRoute>
{
    bool TryGetRoute(string routeKey, out Route route);
}