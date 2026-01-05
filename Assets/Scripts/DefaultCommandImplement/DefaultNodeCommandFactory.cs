public sealed class DefaultNodeCommandFactory : INodeCommandFactory
{
    private readonly IDialogueViewService _dialoguePresentation;
    private readonly ICameraShakeService _cameraShake;

    public DefaultNodeCommandFactory(
        IDialogueViewService dialoguePresentationPort,
        ICameraShakeService cameraShakeService)
    {
        _dialoguePresentation = dialoguePresentationPort;
        _cameraShake = cameraShakeService;
    }
    
    public bool TryCreate(CommandSpecBase spec, out ISequenceCommand command)
    {
        command = null;
        if (spec == null)
            return false;

        switch (spec)
        {
            case ShowLineCommandSpec show:
                command = CreateShowLine(show);
                return command != null;

            case DefaultShakeCameraCommandSpec shake:
                command = CreateShakeCamera(shake);
                return command != null;

            default:
                return false;
        }
    }
    
    
    private ISequenceCommand CreateShowLine(ShowLineCommandSpec  spec)
    {
        if (spec.line == null || _dialoguePresentation == null)
            return null;

        return new DefaultShowLineCommand(_dialoguePresentation, spec.line, spec.screenId, spec.widgetId);
    }

    private ISequenceCommand CreateShakeCamera(DefaultShakeCameraCommandSpec  spec)
    {
        if (_cameraShake == null)
            return null;

        return new ShakeCameraCommand(
            _cameraShake,
            spec.strength,
            spec.duration);
    }
}