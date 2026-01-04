using UnityEngine;

public interface IDialogueNodeOutput
{
    void Show(NodeViewModel vm);
    void Hide();
    void ShowSystemMessage(string msg);
    void PlayStep(NodeSpec node, int stepIndex, NodePlayScope scope, DialogueLine fallbackLine = null);
}

public sealed class DialogueNodeOutputComposite : IDialogueNodeOutput
{
    private readonly IDialoguePresenter _presenter;
    private readonly INodeExecutor _executor;

    public DialogueNodeOutputComposite(IDialoguePresenter presenter, INodeExecutor executor)
    {
        _presenter = presenter;
        _executor = executor;
    }

    public void Show(NodeViewModel vm)
    {
        _presenter?.Present(vm);
    }


    public void Hide() => _presenter?.Hide();
    public void ShowSystemMessage(string msg) => _presenter?.PresentSystemMessage(msg);
    
    public void PlayStep(NodeSpec node, int stepIndex, NodePlayScope scope, DialogueLine fallbackLine = null)
    {
        _executor?.PlayStep(node, stepIndex, scope, fallbackLine);
    }
}
