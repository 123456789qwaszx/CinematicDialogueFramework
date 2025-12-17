using UnityEngine;

public class DialogueTestDriver : MonoBehaviour
{
    [SerializeField] private DialogueBootstrap bootstrap;
    private DialogueStarter _dialogueStarter;

    void Start()
    {
        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<DialogueBootstrap>();
        
        _dialogueStarter = bootstrap.DialogueStarter;
    }
    
    public void StartDialogue()
    {
        _dialogueStarter.StartDialogue("Intro");
    }

}
