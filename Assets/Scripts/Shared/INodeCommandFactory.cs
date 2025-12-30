public interface INodeCommandFactory
{
    bool TryCreate(NodeCommandSpec spec, out ISequenceCommand command);
}