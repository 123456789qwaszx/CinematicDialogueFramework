public interface INodeCommandFactory
{
    /// <summary>
    /// NodeCommandSpec -> runtime ISequenceCommand 생성.
    /// 지원하지 않는 kind/spec면 false 반환.
    /// </summary>
    bool TryCreate(NodeCommandSpec spec, out ISequenceCommand command);
}