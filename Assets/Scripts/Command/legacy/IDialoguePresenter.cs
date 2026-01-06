// /// <summary>
// /// Output port for the dialogue system.
// /// The core uses this interface to present the current dialogue node (or system messages)
// /// </summary>
// public interface IDialoguePresenter
// {
//     // Present a display-oriented snapshot of the current node to the UI.
//     void Present(NodeViewModel vm);
//     
//     // Present a non-dialogue system message (e.g., warnings, debug info, or status text).
//     void PresentSystemMessage(string message);
//     
//     void Hide();
// }