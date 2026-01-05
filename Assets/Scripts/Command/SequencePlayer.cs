using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// - Per-command exception safety (log & continue; never kills the whole run)
/// - Supports non-blocking commands via background coroutines (requires host)
///   and tracks them so Stop() can cancel all fire-and-forget routines at once.
/// </summary>
public sealed class SequencePlayer
{
    private readonly MonoBehaviour _host;
    private readonly List<IEnumerator> _activeBackgroundRoutines = new();

    public SequencePlayer(MonoBehaviour host)
    {
        if (host == null)
            throw new ArgumentNullException(nameof(host));
        _host = host;
    }
    
    public IEnumerator PlayCommands(IReadOnlyList<ISequenceCommand> commands, CommandRunScope scope, int runId,
        Func<bool> isValid, Action<string> trace = null)
    {
        bool Valid() => isValid();

        void Trace(string s) => trace?.Invoke(s);

        int total = commands.Count;
        Trace($"[run:{runId}] PlayCommands begin (count={total})");

        for (int i = 0; i < total; i++)
        {
            if (!Valid())
            {
                Trace($"[run:{runId}] Abort: invalid at idx={i + 1}/{total}");
                yield break;
            }

            if (scope.Token.IsCancellationRequested)
            {
                Trace($"[run:{runId}] Abort: token cancelled at idx={i + 1}/{total}");
                yield break;
            }

            ISequenceCommand command = commands[i];

            string name = GetDebugName(command);
            string tag = $"[run:{runId}][{i + 1}/{total}]";

            IEnumerator routine;
            try
            {
                routine = command.Execute(scope);
            }
            catch (Exception e)
            {
                Trace($"{tag} Exception in Execute(): {name}");
                Debug.LogException(e);
                continue;
            }

            if (routine == null)
            {
                Trace($"{tag} Execute() returned null: {name}");
                continue;
            }

            if (command.WaitForCompletion)
            {
                while (Valid() && !scope.Token.IsCancellationRequested)
                {
                    bool movedNext;

                    try
                    {
                        movedNext = routine.MoveNext();
                    }
                    catch (Exception e)
                    {
                        Trace($"{tag} Exception while running: {name}");
                        Debug.LogException(e);
                        break;
                    }

                    if (!movedNext)
                        break;

                    yield return routine.Current;
                }
            }
            else
            {
                // ---- fire-and-forget commands ----
                // These still run even after PlayCommands yield break
                _activeBackgroundRoutines.Add(routine);
                
                // run(scope)에 귀속: Stop/Skip 시 끊을 수 있게 등록
                scope.Track(
                    cancel: () => { if (routine != null) _host.StopCoroutine(routine); },
                    finish: () => { if (routine != null) _host.StopCoroutine(routine); } // finish는 "멈춤"까지만,
                                                                                         // 완료 상태는 각 커맨드 루틴이 직접 ctx 확인 후, "최종 상태 세팅"을 시행하고 종료할 것
                );

                _host.StartCoroutine(
                    RunBackgroundRoutineToEnd(
                        routine,
                        scope,
                        Valid,
                        onFinished: () => { _activeBackgroundRoutines.Remove(routine); }));
            }
        }

        Trace($"[run:{runId}] PlayCommands end");
    }

    // Stops all background (non-blocking) routines started by this player.
    // Blocking (WaitForCompletion) commands are controlled externally via isValid() and api.Token.
    public void Stop()
    {
        if (_activeBackgroundRoutines.Count <= 0)
            return;

        IEnumerator[] snapshot = _activeBackgroundRoutines.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] != null)
                _host.StopCoroutine(snapshot[i]);
        }

        _activeBackgroundRoutines.Clear();
    }


    private static IEnumerator RunBackgroundRoutineToEnd(
        IEnumerator routine,
        CommandRunScope ctx,
        Func<bool> isValid,
        Action onFinished)
    {
        try
        {
            while (isValid() && !ctx.Token.IsCancellationRequested)
            {
                bool movedNext;

                try
                {
                    movedNext = routine.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    yield break;
                }

                if (!movedNext) yield break;

                yield return routine.Current;
            }
        }
        finally
        {
            onFinished?.Invoke();
        }
    }

    private static string GetDebugName(ISequenceCommand command)
    {
        if (command is CommandBase commandBase)
            return commandBase.DebugName;

        return command.GetType().Name;
    }
}