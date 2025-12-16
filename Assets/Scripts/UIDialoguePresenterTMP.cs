using TMPro;
using UnityEngine;

public sealed class UIDialoguePresenterTMP : MonoBehaviour, IDialoguePresenter
{
    [SerializeField] private TMP_Text speaker;
    [SerializeField] private TMP_Text body;

    public void Present(NodeViewModel viewModel)
    {
        if (speaker)
            speaker.text = viewModel.SpeakerId;
        
        if (body)
            body.text = viewModel.Text;
    }

    public void PresentSystemMessage(string message)
    {
        if (speaker)
            speaker.text = "System";
        
        if (body)
            body.text = message;
    }

    public void Clear()
    {
        if (speaker)
            speaker.text = "";
        
        if (body)
            body.text = "";
    }
}