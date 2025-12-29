public sealed class DefaultNodeCommandFactory : INodeCommandFactory
{
    public bool TryCreate(NodeCommandSpec spec, out ISequenceCommand command)
    {
        command = null;
        
        if (spec == null)
            return false;

        switch (spec.kind)
        {
            case NodeCommandKind.ShowLine:
                command = new ShowLineCommand(spec.line);
                return true;

            case NodeCommandKind.ShakeCamera:
                command = new ShakeCameraCommand
                {
                    strength = spec.shakeStrength,
                    duration = spec.shakeDuration
                };
                return true;

            default:
                return false;
        }
    }
}