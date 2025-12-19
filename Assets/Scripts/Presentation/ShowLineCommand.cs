using System.Collections;

public sealed class ShowLineCommand : CommandBase
{
    private readonly DialogueLine _line;

    public ShowLineCommand(DialogueLine line) => _line = line;

    public override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;

    public override string DebugName
        => $"ShowLine(speaker={_line?.speakerId}, len={_line?.text?.Length ?? 0})";

    public override bool WaitForCompletion => true;

    protected override IEnumerator ExecuteInner(CommandContext ctx)
    {
        var routine = ctx.ShowLine(_line);
        if (routine != null)
            yield return routine;
    }

    protected override void OnSkip(CommandContext ctx)
    {
        ctx.ShowLineImmediate(_line);
    }
}