using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single, canonical command runner:
/// - sequential execution
/// - exception-safe per command (log & continue)
/// - cancellation-aware
/// - supports non-blocking commands via background coroutines (requires host)
/// </summary>
public sealed class SequencePlayer
{
    private readonly MonoBehaviour _host;
    private readonly List<Coroutine> _bg = new();
    private bool _isStopping;

    public SequencePlayer(MonoBehaviour host)
    {
        _host = host;
    }

    /// <summary>Stops all background routines started by non-blocking commands.</summary>
    public void Stop()
    {
        if (_host == null) { _bg.Clear(); return; }

        _isStopping = true;
        try
        {
            if (_bg.Count <= 0) return;

            var snapshot = _bg.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null)
                    _host.StopCoroutine(snapshot[i]);
            }
            _bg.Clear();
        }
        finally
        {
            _isStopping = false;
        }
    }

    /// <summary>
    /// Plays commands sequentially.
    /// isValid: optional guard (e.g., runId check) to stop stale routines safely.
    /// trace: optional logger sink (StringBuilder/Debug UI/Debug.Log).
    /// </summary>
    public IEnumerator PlayCommands(
        IReadOnlyList<ISequenceCommand> commands,
        NodePlayScope ctx,
        int runId,
        Func<bool> isValid = null,
        Action<string> trace = null)
    {
        if (commands == null || ctx == null) yield break;

        bool Valid() => isValid == null || isValid();
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

            if (ctx.Token.IsCancellationRequested)
            {
                Trace($"[run:{runId}] Abort: token cancelled at idx={i + 1}/{total}");
                yield break;
            }

            var cmd = commands[i];
            if (cmd == null)
            {
                Trace($"[run:{runId}][{i + 1}/{total}] Skip null command");
                continue;
            }

            string name = GetDebugName(cmd);
            string tag = $"[run:{runId}][{i + 1}/{total}]";

            IEnumerator routine;
            try
            {
                Trace($"{tag} Execute: {name} (wait={cmd.WaitForCompletion})");
                routine = cmd.Execute(ctx);
            }
            catch (Exception e)
            {
                Trace($"{tag} Exception in Execute(): {name}");
                Debug.LogException(e);
                continue;
            }

            if (routine == null)
            {
                Trace($"{tag} No routine returned: {name}");
                continue;
            }

            if (cmd.WaitForCompletion)
            {
                // ✅ IMPORTANT: yield return cannot be inside try/catch.
                int startFrame = Time.frameCount;
                float startT = Time.realtimeSinceStartup;

                bool hadYield = false;
                bool exceptionThrown = false;

                while (Valid() && !ctx.Token.IsCancellationRequested)
                {
                    bool movedNext;

                    // try/catch is ONLY around MoveNext()
                    try
                    {
                        movedNext = routine.MoveNext();
                    }
                    catch (Exception e)
                    {
                        exceptionThrown = true;
                        Trace($"{tag} Exception while running: {name}");
                        Debug.LogException(e);
                        break;
                    }

                    if (!movedNext)
                        break;

                    hadYield = true;
                    yield return routine.Current;
                }

                int endFrame = Time.frameCount;
                float endT = Time.realtimeSinceStartup;

                Trace($"{tag} Done: {name} (yielded={hadYield}, exception={exceptionThrown}, frames={endFrame - startFrame}, sec={(endT - startT):0.000})");
            }
            else
            {
                // fire-and-forget but MUST execute
                if (_host != null)
                {
                    int startFrame = Time.frameCount;
                    float startT = Time.realtimeSinceStartup;

                    // ✅ log start BEFORE StartCoroutine to avoid "finished before start" ordering
                    Trace($"{tag} BG start: {name} (frame={startFrame})");

                    Coroutine c = null;
                    c = _host.StartCoroutine(RunToEndBackground(
                        routine,
                        ctx,
                        Valid,
                        onFinished: () =>
                        {
                            if (_isStopping) return;

                            _bg.Remove(c);

                            int endFrame = Time.frameCount;
                            float endT = Time.realtimeSinceStartup;

                            Trace($"{tag} BG finished: {name} (frames={endFrame - startFrame}, sec={(endT - startT):0.000})");
                        },
                        onException: (ex) =>
                        {
                            Trace($"{tag} BG exception: {name}");
                            Debug.LogException(ex);
                        }
                    ));

                    _bg.Add(c);
                }
                else
                {
                    // no host => cannot truly run background coroutine
                    bool movedNext = false;
                    Exception ex = null;

                    try
                    {
                        movedNext = routine.MoveNext();
                    }
                    catch (Exception e)
                    {
                        ex = e;
                        Debug.LogException(e);
                    }

                    if (ex != null)
                        Trace($"{tag} WARN: non-blocking threw but no host: {name}");
                    else if (movedNext)
                        Trace($"{tag} WARN: non-blocking yielded but no host: {name}");
                }
            }
        }

        Trace($"[run:{runId}] PlayCommands end");
    }

    private static IEnumerator RunToEndBackground(
        IEnumerator routine,
        NodePlayScope ctx,
        Func<bool> isValid,
        Action onFinished,
        Action<Exception> onException)
    {
        try
        {
            while ((isValid == null || isValid()) &&
                   !ctx.Token.IsCancellationRequested)
            {
                bool movedNext;

                try
                {
                    movedNext = routine.MoveNext();
                }
                catch (Exception e)
                {
                    onException?.Invoke(e);
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

    private static string GetDebugName(ISequenceCommand cmd)
    {
        if (cmd is CommandBase cb) return cb.DebugName;
        return cmd.GetType().Name;
    }
}
