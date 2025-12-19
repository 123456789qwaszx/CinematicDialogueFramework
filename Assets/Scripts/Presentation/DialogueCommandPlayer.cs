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
            if (_bg.Count > 0)
            {
                var snapshot = _bg.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    if (snapshot[i] != null)
                        _host.StopCoroutine(snapshot[i]);
                }
                _bg.Clear();
            }
        }
        finally
        {
            _isStopping = false;
        }
    }

    /// <summary>
    /// Plays commands sequentially.
    /// isValid: optional guard (e.g., runId check) to stop stale routines safely.
    /// </summary>
    public IEnumerator PlayCommands(
        IReadOnlyList<ISequenceCommand> commands,
        NodePlayScope ctx,
        Func<bool> isValid = null,
        bool log = false)
    {
        if (commands == null || ctx == null) yield break;

        bool Valid() => isValid == null || isValid();

        for (int i = 0; i < commands.Count; i++)
        {
            if (!Valid()) yield break;
            if (ctx.Token.IsCancellationRequested) yield break;

            var cmd = commands[i];
            if (cmd == null) continue;

            IEnumerator routine = null;
            try
            {
                if (log) Debug.Log($"[SequencePlayer] Execute: {GetDebugName(cmd)} (wait={cmd.WaitForCompletion})");
                routine = cmd.Execute(ctx);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                continue; // one command fails -> continue next
            }

            if (routine == null) continue;

            if (cmd.WaitForCompletion)
            {
                while (Valid() && !ctx.Token.IsCancellationRequested && routine.MoveNext())
                    yield return routine.Current;
            }
            else
            {
                // fire-and-forget but MUST execute
                if (_host != null)
                {
                    Coroutine c = null;
                    c = _host.StartCoroutine(RunToEndBackground(routine, ctx, Valid, () =>
                    {
                        if (_isStopping) return;
                        _bg.Remove(c);
                    }));
                    _bg.Add(c);
                }
                else
                {
                    // no coroutine host: at least try one MoveNext to trigger side-effects
                    bool yielded = false;
                    try { yielded = routine.MoveNext(); }
                    catch (Exception e) { Debug.LogException(e); }

                    if (yielded)
                        Debug.LogWarning("[SequencePlayer] Non-blocking command yielded but there is no coroutine host.");
                }
            }
        }
    }

    private static IEnumerator RunToEndBackground(
        IEnumerator routine,
        NodePlayScope ctx,
        Func<bool> isValid,
        Action onFinished)
    {
        try
        {
            while ((isValid == null || isValid()) &&
                   !ctx.Token.IsCancellationRequested &&
                   routine.MoveNext())
            {
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
