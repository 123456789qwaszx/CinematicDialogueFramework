using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SequencePlayer
{
    private readonly MonoBehaviour _host;
    private readonly List<Coroutine> _bg = new();
    private bool _isStopping;

    public SequencePlayer(MonoBehaviour host) => _host = host;

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

    public IEnumerator PlayCommands(
        IReadOnlyList<ISequenceCommand> commands,
        NodePlayScope ctx,
        Func<bool> isValid = null,
        Action<string> trace = null)
    {
        if (commands == null || ctx == null) yield break;

        bool Valid() => isValid == null || isValid();
        void Trace(string s) => trace?.Invoke(s);

        for (int i = 0; i < commands.Count; i++)
        {
            if (!Valid()) yield break;
            if (ctx.Token.IsCancellationRequested) yield break;

            var cmd = commands[i];
            if (cmd == null) continue;

            IEnumerator routine = null;
            try
            {
                Trace($"Execute: {GetDebugName(cmd)} (wait={cmd.WaitForCompletion})");
                routine = cmd.Execute(ctx);
            }
            catch (Exception e)
            {
                Trace($"Exception in Execute(): {GetDebugName(cmd)}");
                Debug.LogException(e);
                continue;
            }

            if (routine == null) continue;

            if (cmd.WaitForCompletion)
            {
                while (Valid() && !ctx.Token.IsCancellationRequested && routine.MoveNext())
                    yield return routine.Current;
            }
            else
            {
                if (_host != null)
                {
                    Coroutine c = null;
                    c = _host.StartCoroutine(RunToEndBackground(routine, ctx, Valid, () =>
                    {
                        if (_isStopping) return;
                        _bg.Remove(c);
                        Trace($"BG finished: {GetDebugName(cmd)}");
                    }));
                    _bg.Add(c);

                    Trace($"BG start: {GetDebugName(cmd)}");
                }
                else
                {
                    bool yielded = false;
                    try { yielded = routine.MoveNext(); }
                    catch (Exception e) { Debug.LogException(e); }

                    if (yielded)
                        Trace("Non-blocking command yielded but there is no coroutine host.");
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
