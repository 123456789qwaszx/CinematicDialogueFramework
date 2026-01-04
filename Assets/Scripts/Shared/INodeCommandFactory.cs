public interface INodeCommandFactory
{
    bool TryCreate(CommandSpecBase spec, out ISequenceCommand command);
}