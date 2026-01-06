using System.Collections;

public interface ISequenceCommand
{
    bool WaitForCompletion { get; }

    IEnumerator Execute(CommandRunScope api);
}