using UnityEngine;

public interface IDialogueNodeOutput
{
    void Show(NodeViewModel vm);         // 표시(스냅샷)
    void Play(DialogueNodeSpec node, DialogueContext ctx, DialogueLine fallbackLine = null); // 연출 실행(선택)
    void Clear();
    void ShowSystemMessage(string msg);
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
        Debug.Log($"{_presenter}");
        _presenter?.Present(vm);
    }

    public void Play(DialogueNodeSpec node, DialogueContext ctx, DialogueLine fallbackLine = null)
    {
        Debug.Log($"{_executor}");
        _executor?.Play(node, ctx, fallbackLine);
    }

    public void Clear() => _presenter?.Clear();
    public void ShowSystemMessage(string msg) => _presenter?.PresentSystemMessage(msg);
}
