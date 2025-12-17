using UnityEngine;

public class DialogueStarter 
{
    private readonly DialogueSession _session;

    public DialogueStarter(DialogueSession session) => _session = session;

    public void StartDialogue(string routeKey, object payload = null)
    {
        _session.StartDialogue(routeKey);
    }
}
