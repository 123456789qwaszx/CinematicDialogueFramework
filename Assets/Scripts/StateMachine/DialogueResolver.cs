public class DialogueResolver
{
    private readonly IDialogueRouteCatalog _routes;

    public DialogueResolver(IDialogueRouteCatalog routes)
    {
        _routes = routes;
    }

    private const string FallbackRouteKey = "Default";
    
    public SituationSpec Resolve(string routeKey)
    {
        _routes.TryGetRoute(routeKey, out DialogueRoute route);
        //DialogueRoute route = _routes.GetRoute(routeKey);
        DialogueSequenceData sequence = route.Sequence;
        string situationKey = route.SituationKey;

        if (!sequence.TryGetSituation(situationKey, out SituationSpec situation))
        {
            if (!sequence.TryGetSituation(FallbackRouteKey, out situation))
            {
                return null;
            }
        }

        return situation;
    }
}