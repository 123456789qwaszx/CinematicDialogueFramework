using System.Collections;

public sealed class ShowLineCommand : CommandBase
{
    private readonly IDialogueViewService _presentation;
    private readonly DialogueLine _line;
    
    public ShowLineCommand(IDialogueViewService presentation, DialogueLine line)
    {
        _presentation = presentation;
        _line = line;
    }


    public override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;

    protected override IEnumerator ExecuteInner()
    {
        IEnumerator routine = _presentation.ShowLine(_line);
        
        if (routine != null)
            yield return routine;
    }

    protected override void OnSkip(NodePlayScope api)
    {
        _presentation.ShowLineImmediate(_line);
    }
}