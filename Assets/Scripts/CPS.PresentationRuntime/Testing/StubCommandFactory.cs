// using System;
// using System.Collections;
// using UnityEngine;
//
// public class StubCommandFactory : INodeCommandFactory
// {
//     private readonly ITimeSource _time;
//     private readonly ISignalBus _signal;
//     private readonly ISignalLatch _latch;
//     
//     public StubCommandFactory(ITimeSource time, ISignalBus signal, ISignalLatch latch)
//     {
//         _time   = time;
//         _signal = signal;
//         _latch = latch;
//     }
//     
//     public bool TryCreate(CommandSpecBase spec, out ISequenceCommand command)
//     {
//         command = null;
//         if (spec == null) return false;
//
//         switch (spec)
//         {
//             case StubCommandSpec s:
//                 command = new StubCommand();
//                 return true;
//
//             case LogCommandSpec s:
//                 command = new LogCommand(
//                     message: s.message,
//                     tag: s.tag,
//                     includeFrame: s.includeFrame
//                 );
//                 return true;
//
//             case LogWaitCommandSpec s:
//                 command = new LogWaitCommand(
//                     time: _time,
//                     seconds: s.seconds,
//                     message: s.message
//                 );
//                 return true;
//
//             case LogSignalCommandSpec s:
//                 command = new LogSignalCommand(
//                     signal: _signal,
//                     key: s.signalKey,
//                     raise: s.raise,
//                     message: s.message
//                 );
//                 return true;
//
//             default:
//                 return false;
//         }
//     }
// }
//
//
// [Serializable]
// [CommandRouting(CommandTrackType.Dialogue, CommandPhase.Dialogue)]
// [CommandTimingHint(blocking: true, infinite: false, duration: 0.0f)] // TypeText처럼 보이게: blocking=true
// [CommandMenuHint(
//     "Stub",
//     "Stub Type",
//     Sets = new[] { "Debug/Stub" },
//     SetOrder = 40,
//     Order = 40)]
// public sealed class StubCommandSpec : CommandSpecBase
// {
//     public string text;
//     public float charInterval = 0.03f;
//     public bool wait = true;
// }
//
//
// public sealed class StubCommand : CommandBase
// {
//     public StubCommand() { }
//     
//     public override bool WaitForCompletion => false;
//     protected override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
//     
//     protected override IEnumerator ExecuteInner(CommandRunScope scope)
//     {
//         Debug.Log("StubCommand");
//         yield break;
//     }
// }
//
// [Serializable]
// [CommandRouting(CommandTrackType.Dialogue, CommandPhase.Dialogue)]
// [CommandTimingHint(blocking: false, infinite: true, duration: 0.0f)] // INF 배지 테스트
// [CommandMenuHint("Stub", "Stub Infinite (Test)", Sets = new[] { "Debug/Stub" }, Order = 41)]
// public sealed class StubInfiniteCommandSpec : CommandSpecBase
// {
//     public string text;
// }
//
// [Serializable]
// [CommandRouting(CommandTrackType.Dialogue, CommandPhase.Setup)]           // Phase 섞임 테스트
// [CommandTimingHint(blocking: false, infinite: false, duration: 0.0f)]
// [CommandMenuHint("Debug", "Log (Setup Phase)", Sets = new[] { "Debug/Log" }, Order = 1)]
// public sealed class LogCommandSpec : CommandSpecBase
// {
//     [TextArea] public string message = "Hello CPS!";
//     public string tag = "Debug";
//     public bool includeFrame = true;
// }
//
// public sealed class LogCommand : CommandBase
// {
//     private readonly string _message;
//     private readonly string _tag;
//     private readonly bool _includeFrame;
//
//     public LogCommand(string message, string tag, bool includeFrame)
//     {
//         _message = message ?? "";
//         _tag = string.IsNullOrWhiteSpace(tag) ? "Debug" : tag;
//         _includeFrame = includeFrame;
//     }
//
//     public override bool WaitForCompletion => false;
//     protected override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
//
//     protected override IEnumerator ExecuteInner(CommandRunScope scope)
//     {
//         string suffix = _includeFrame ? $" (frame={Time.frameCount})" : "";
//         Debug.Log($"[{_tag}] {_message}{suffix}");
//         yield break;
//     }
// }
//
//
//
// [Serializable]
// [CommandRouting(CommandTrackType.Interaction, CommandPhase.Setup)]
// [CommandTimingHint(blocking: true, infinite: false, duration: 0.5f)]      // duration 배지/막대 테스트
// [CommandMenuHint("Debug", "Log Wait", Sets = new[] { "Debug/Timing" }, Order = 2)]
// public sealed class LogWaitCommandSpec : CommandSpecBase
// {
//     [TextArea] public string message = "Waited!";
//     public float seconds = 0.5f;
// }
//
// public sealed class LogWaitCommand : CommandBase
// {
//     private readonly ITimeSource _time;
//     private readonly float _seconds;
//     private readonly string _message;
//
//     public LogWaitCommand(ITimeSource time, float seconds, string message)
//     {
//         _time = time;
//         _seconds = Mathf.Max(0f, seconds);
//         _message = message ?? "";
//     }
//
//     public override bool WaitForCompletion => true;
//     protected override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
//
//     protected override IEnumerator ExecuteInner(CommandRunScope scope)
//     {
//         Debug.Log($"[LogWait] begin: {_seconds:0.###}s | {_message}");
//
//         // time source로 기다리기 (네 ITimeSource 구현에 맞춰 한 프레임씩)
//         float start = _time.UnscaledDeltaTime;
//         while ((_time.UnscaledDeltaTime - start) < _seconds)
//         {
//             if (scope.Token.IsCancellationRequested)
//                 yield break;
//
//             yield return null;
//         }
//
//         Debug.Log($"[LogWait] end: {_message}");
//     }
// }
//
//
//
// [Serializable]
// [CommandRouting(CommandTrackType.Interaction, CommandPhase.Setup)]
// [CommandTimingHint(blocking: false, infinite: true, duration: 0.0f)]      // INF 배지 테스트
// [CommandMenuHint("Debug", "Log Signal (INF Test)", Sets = new[] { "Debug/Signal" }, Order = 4)]
// public sealed class LogSignalInfiniteSpec : CommandSpecBase
// {
//     public string signalKey = "test.signal";
//     [TextArea] public string message = "Signal INF Test";
// }
//
// [Serializable]
// [CommandRouting(CommandTrackType.Interaction, CommandPhase.Setup)]
// [CommandTimingHint(blocking: false, infinite: false, duration: 0.0f)]
// [CommandMenuHint("Debug", "Log Signal", Sets = new[] { "Debug/Signal" }, Order = 3)]
// public sealed class LogSignalCommandSpec : CommandSpecBase
// {
//     public string signalKey = "test.signal";
//     public bool raise = true;
//
//     [TextArea] public string message = "Signal!";
// }
// public sealed class LogSignalCommand : CommandBase
// {
//     private readonly ISignalBus _signal;
//     private readonly string _key;
//     private readonly bool _raise;
//     private readonly string _message;
//
//     public LogSignalCommand(ISignalBus signal, string key, bool raise, string message)
//     {
//         _signal = signal;
//         _key = key ?? "";
//         _raise = raise;
//         _message = message ?? "";
//     }
//
//     public override bool WaitForCompletion => false;
//     protected override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
//
//     protected override IEnumerator ExecuteInner(CommandRunScope scope)
//     {
//         Debug.Log($"[LogSignal] {_message} | key='{_key}' | raise={_raise}");
//
//         if (_raise && !string.IsNullOrWhiteSpace(_key))
//             _signal.Raise(_key);
//
//         yield break;
//     }
//     
//     [Serializable]
//     [CommandRouting(CommandTrackType.Dialogue, CommandPhase.FX)]              // track 의도적 꼬임
//     [CommandTimingHint(blocking: false, infinite: false, duration: 0.0f)]
//     [CommandMenuHint("Debug", "Drift Probe", Sets = new[] { "Debug/Drift" }, Order = 99)]
//     public sealed class DriftProbeCommandSpec : CommandSpecBase
//     {
//         public string note = "Put this in FX track list, but meta.track says Dialogue.";
//     }
// }