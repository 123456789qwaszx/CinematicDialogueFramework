using TMPro;
using UnityEngine;

public sealed class UIDialoguePresenterTMP : MonoBehaviour, IDialoguePresenter
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text speaker;
    [SerializeField] private TMP_Text body;

    public void Present(NodeViewModel viewModel)
    {
        if (root) root.SetActive(true);

        if (speaker)
            speaker.text = viewModel.SpeakerId ?? "";
        
        if (body)
            body.text = viewModel.Text ?? "";
    }

    public void PresentSystemMessage(string message)
    {
        if (root)
            root.SetActive(true);

        if (speaker)
            speaker.text = "System";
        
        if (body)
            body.text = message ?? "";
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    public void ClearText()
    {
        if (speaker) speaker.text = "";
        if (body) body.text = "";
    }
    
}