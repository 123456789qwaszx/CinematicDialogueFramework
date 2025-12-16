
using System.Collections.Generic;
using System.Collections;


public class SequencePlayer
{
    public IEnumerator PlayCommands(
        IReadOnlyList<ISequenceCommand> commands,
        CommandContext ctx)
    {
        foreach (var cmd in commands)
        {
            var routine = cmd.Execute(ctx);

            if (cmd.WaitForCompletion && routine != null)
            {
                // 끝날 때까지 기다림
                while (routine.MoveNext())
                    yield return routine.Current;
            }
            // WaitForCompletion == false 면 그냥 fire-and-forget 식으로
        }
    }
}