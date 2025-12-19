public class DialogueResolver
{
    private readonly IDialogueRouteCatalog _routes;

    public DialogueResolver(IDialogueRouteCatalog routes)
    {
        _routes = routes;
    }

    private const string FallbackRouteKey = "Default";
    
    public SituationSpecSO Resolve(string routeKey)
    {
        if (!_routes.TryGetRoute(routeKey, out DialogueRoute route))
        {
            if (!_routes.TryGetRoute(FallbackRouteKey, out route))
            {
                return null;
            }
        }
        
        DialogueSequenceData sequence = route.Sequence;
        string situationKey = route.SituationKey;

        if (!sequence.TryGetSituation(situationKey, out SituationSpecSO situation))
        {
            if (!sequence.TryGetSituation(FallbackRouteKey, out situation))
            {
                return null;
            }
        }

        return situation;
    }
}