using System.Collections;
using System.Threading;
using UnityEngine;




public abstract class CommandBase : ISequenceCommand
{
    public virtual bool WaitForCompletion => true;
    public virtual string DebugName => GetType().Name;

    public virtual SkipPolicy SkipPolicy => SkipPolicy.Ignore;

    public IEnumerator Execute(NodePlayScope api)
    {
        if (api == null) yield break;
        if (api.Token.IsCancellationRequested) yield break;

        if (api.IsSkipping)
        {
            switch (SkipPolicy)
            {
                case SkipPolicy.Ignore:
                    yield break;

                case SkipPolicy.CompleteImmediately:
                    try { OnSkip(api); }
                    catch (System.Exception e) { Debug.LogException(e); }
                    yield break;

                case SkipPolicy.ExecuteEvenIfSkipping:
                    break;
            }
        }

        IEnumerator inner = null;
        try { inner = ExecuteInner(api); }
        catch (System.Exception e) { Debug.LogException(e); yield break; }

        if (inner != null) yield return inner;
    }

    protected abstract IEnumerator ExecuteInner(NodePlayScope api);

    protected virtual void OnSkip(NodePlayScope api) { }

    protected IEnumerator Wait(NodePlayScope api, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (api.Token.IsCancellationRequested) yield break;
            if (api.IsSkipping) yield break;

            // GateRunner와 동일 철학: unscaled + api.TimeScale
            t += Time.unscaledDeltaTime * api.TimeScale;
            yield return null;
        }
    }
}

