using System.Collections.Generic;

public class DialogueResolver
{
    private readonly IDialogueRouteCatalog _routes;

    public DialogueResolver(IDialogueRouteCatalog routes)
    {
        _routes = routes;
    }

    public SituationSpec Resolve(string routeKey)
    {
        DialogueRoute route = _routes.GetRoute(routeKey);
        DialogueSequenceData sequence = route.Sequence;
        string situationKey = route.SituationKey;
        
        SituationSpec situation = sequence.GetSituation(situationKey);

        
        return situation;
    }

}
