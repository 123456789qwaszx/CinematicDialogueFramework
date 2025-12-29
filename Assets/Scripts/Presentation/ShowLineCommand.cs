using System.Collections;

public sealed class ShowLineCommand : CommandBase
{
    private readonly DialogueLine _line;
    public ShowLineCommand(DialogueLine line) => _line = line;

    public override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;

    protected override IEnumerator ExecuteInner(NodePlayScope scope)
    {
        var routine = scope.Presenter.ShowLine(_line);
        if (routine != null) yield return routine;
    }

    protected override void OnSkip(NodePlayScope scope)
    {
        scope.Presenter.ShowLineImmediate(_line);
    }
}