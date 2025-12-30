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
    
    public bool TryCreate(NodeCommandSpec spec, out ISequenceCommand command)
    {
        command = null;
        if (spec == null)
            return false;

        switch (spec.kind)
        {
            case NodeCommandKind.ShowLine:
                command = CreateShowLine(spec);
                return command != null;

            case NodeCommandKind.ShakeCamera:
                command = CreateShakeCamera(spec);
                return command != null;

            default:
                return false;
        }
    }
    
    
    private ISequenceCommand CreateShowLine(NodeCommandSpec spec)
    {
        if (spec.line == null || _dialoguePresentation == null)
            return null;

        return new ShowLineCommand(_dialoguePresentation, spec.line);
    }

    private ISequenceCommand CreateShakeCamera(NodeCommandSpec spec)
    {
        if (_cameraShake == null)
            return null;

        return new ShakeCameraCommand(
            _cameraShake,
            spec.shakeStrength,
            spec.shakeDuration);
    }
}