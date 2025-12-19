using UnityEngine;

public class DialogueStarter 
{
    private readonly DialogueSession _session;
    public DialogueSession Session => _session;

    public DialogueStarter(DialogueSession session) => _session = session;

    public void StartDialogue(string routeKey, object payload = null)
    {
        _session.StartDialogue(routeKey);
    }
    
    public void Stop()
    {
        _session.Stop();
    }
    
    public void Restart(string routeKey, object payload = null)
    {
        _session.Stop();
        _session.StartDialogue(routeKey);
    }
    
    public bool ToggleSkip()
    {
        _session.Context.IsSkipping = !_session.Context.IsSkipping;
        Debug.Log($"[DialogueStarter] IsSkipping={_session.Context.IsSkipping}");
        return _session.Context.IsSkipping;
    }
    
    public bool ToggleAutoMode()
    {
        _session.Context.IsAutoMode = !_session.Context.IsAutoMode;
        Debug.Log($"[DialogueStarter] IsAutoMode={_session.Context.IsAutoMode}");
        return _session.Context.IsAutoMode;
    }
    
    public void SetAutoDelay(float seconds)
    {
        _session.Context.AutoAdvanceDelay = Mathf.Max(0f, seconds);
        Debug.Log($"[DialogueStarter] AutoAdvanceDelay={_session.Context.AutoAdvanceDelay:0.00}s");
    }
    
    public void SetTimeScale(float scale)
    {
        _session.Context.TimeScale = Mathf.Max(0.01f, scale);
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
