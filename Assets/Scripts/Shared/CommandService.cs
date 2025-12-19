using System;
using System.Collections;
using UnityEngine;

public class CommandService : IPresentationPort
{
    private readonly CommandServiceConfig _config;
    public CommandService(CommandServiceConfig config)
    {
        _config = config;
    }

    private ICameraShakeService _cameraShake => _config?.CameraShakeService;
    private IDialogueViewService _dialogueView => _config?.DialogueViewService;
    
    public void ShakeCamera(float strength, float duration)
        => _cameraShake?.Shake(strength, duration);


    public IEnumerator ShowLine(DialogueLine line)
    {
        return _dialogueView != null
            ? _dialogueView.ShowLine(line)
            : null;
    }

    public void ShowLineImmediate(DialogueLine line)
        => _dialogueView?.ShowLineImmediate(line);
}

