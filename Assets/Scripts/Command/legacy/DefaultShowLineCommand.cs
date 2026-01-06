// using System.Collections;
//
// public sealed class DefaultShowLineCommand : CommandBase
// {
//     private readonly IDialogueViewService _presentation;
//     private readonly DialogueLine _line;
//     private readonly string _screenId;
//     private readonly string _widgetId;
//     
//     public DefaultShowLineCommand(IDialogueViewService presentation, DialogueLine line, string screenId, string widgetId)
//     {
//         _presentation = presentation;
//         _line = line;
//         _screenId = screenId;
//         _widgetId = widgetId;
//     }
//
//     protected override IEnumerator ExecuteInner(CommandRunScope scope)
//     {
//         IEnumerator routine = _presentation.ShowLine(_line,_screenId, _widgetId);
//         
//         if (routine != null)
//             yield return routine;
//     }
//
//     protected override void OnSkip(CommandRunScope api)
//     {
//         _presentation.ShowLineImmediate(_line,_screenId, _widgetId);
//     }
// }