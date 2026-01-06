using UnityEngine;

public class DialogueStarter 
{
    private readonly PresentationSession _session;
    public PresentationSession Session => _session;

    public DialogueStarter(PresentationSession session) => _session = session;

    public void StartDialogue(string routeKey, object payload = null)
    {
        _session.Start(routeKey);
    }
    
    public void Stop()
    {
        _session.EndDialogue();
    }
    
    public void Restart(string routeKey, object payload = null)
    {
        _session.EndDialogue();
        _session.Start(routeKey);
    }
    
    public bool ToggleSkip()
    {
        _session.Context.Modes.IsSkipping = !_session.Context.Modes.IsSkipping;
        Debug.Log($"[DialogueStarter] IsSkipping={_session.Context.IsSkipping}");
        return _session.Context.IsSkipping;
    }
    
    public bool ToggleAutoMode()
    {
        _session.Context.Modes.IsAutoMode = !_session.Context.Modes.IsAutoMode;
        Debug.Log($"[DialogueStarter] IsAutoMode={_session.Context.IsAutoMode}");
        return _session.Context.IsAutoMode;
    }
    
    public void SetAutoDelay(float seconds)
    {
        _session.Context.Modes.AutoAdvanceDelay = Mathf.Max(0f, seconds);
        Debug.Log($"[DialogueStarter] AutoAdvanceDelay={_session.Context.AutoAdvanceDelay:0.00}s");
    }
    
    public void SetTimeScale(float scale)
    {
        _session.Context.Modes.TimeScale = Mathf.Max(0.01f, scale);
        Debug.Log($"[DialogueStarter] TimeScale={_session.Context.TimeScale:0.00}");
    }
    
    public void DumpState(string label = null)
    {
        var ctx = _session.Context;
        Debug.Log(
            $"[DialogueStarter]{(string.IsNullOrEmpty(label) ? "" : $" {label}")} " +
            $"Auto={ctx.IsAutoMode}, Skip={ctx.IsSkipping}, TimeScale={ctx.TimeScale:0.00}, AutoDelay={ctx.AutoAdvanceDelay:0.00}"
        );
    }
}
