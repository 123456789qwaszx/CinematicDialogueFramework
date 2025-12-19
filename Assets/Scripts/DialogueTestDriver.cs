using UnityEngine;

public class DialogueTestDriver : MonoBehaviour
{
    [SerializeField] private DialogueBootstrap bootstrap;
    private DialogueStarter _dialogueStarter;

    [SerializeField] private string testRouteKey = "Intro";
    [SerializeField] private float delaySecond = 0.6f;
    [SerializeField] private float timeScale = 1f;

    void Start()
    {
        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<DialogueBootstrap>();
        
        _dialogueStarter = bootstrap.DialogueStarter;
    }
    
    public void StartDialogue()
    {
        _dialogueStarter.StartDialogue(testRouteKey);
    }
    
    public void Stop()
    {
        _dialogueStarter.Stop();
    }
    
    public void Restart()
    {
        _dialogueStarter.Stop();
        _dialogueStarter.StartDialogue(testRouteKey);
    }
    
    public void ToggleSkip()
    {
        _dialogueStarter.ToggleSkip();
    }
    
    
    public void ToggleAutoMode()
    {
        _dialogueStarter.ToggleAutoMode();
    }

    
    public void SetAutoDelay()
    {
        _dialogueStarter.SetAutoDelay(delaySecond);
    }
    
    
    public void SetTimeScale()
    {
        _dialogueStarter.SetTimeScale(timeScale);
    }
    
    public void DumpState()
    {
        _dialogueStarter.DumpState(null);
    }
    
}
