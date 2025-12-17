using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

public class ShowLineCommand : CommandBase
{
    private readonly DialogueLine _line;
    public ShowLineCommand(DialogueLine line) => _line = line;

    public override bool WaitForCompletion => true;

    protected override IEnumerator ExecuteInner(CommandContext ctx)
    {
        Debug.Log("ShowLineCommand");
        // if (ctx.IsSkipping)
        // {
        //     ctx.ShowLineImmediate(_line);
        //     yield break;
        // }

        IEnumerator routine = ctx.ShowLine(_line);
        if (routine != null)
            yield return routine;
    }

    protected override void OnSkip(CommandContext ctx)
    {
        ctx.ShowLineImmediate(_line);
    }
}


// public class ShowLineCommand : CommandBase
// {
//     private readonly DialogueLine _line;
//
//     public ShowLineCommand(DialogueLine line)
//     {
//         _line = line;
//     }
//
//     public override string DebugName => $"ShowLine({_line.speakerId}: \"{_line.text}\")";
//
//     // 대사를 다 보여줄 때까지 기다리는 커맨드
//     public override bool WaitForCompletion => true;
//
//     protected override IEnumerator ExecuteInner(CommandContext ctx)
//     {
//         Debug.Log(_line.speakerId);
//         Debug.Log("text");
//         
//         if (ctx.IsSkipping)
//         {
//             // 스킵 모드면 즉시 완성 상태로 보여주기
//             ctx.ShowLineImmediate(_line);
//             yield break;
//         }
//
//         // 일반 모드: 타이핑 + 입력(or 오토)까지 기다리는 코루틴
//         var routine = ctx.ShowLine(_line);
//         if (routine != null)
//         yield return routine;
//     }
//
//     protected override void OnSkip(CommandContext ctx)
//     {
//         // 스킵 전용 동작: 그냥 즉시 완성 상태로 뿌려버림
//         ctx.ShowLineImmediate(_line);
//     }
// }