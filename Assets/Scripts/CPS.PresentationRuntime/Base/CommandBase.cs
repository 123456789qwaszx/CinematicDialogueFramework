using System.Collections;
using UnityEngine;

public abstract class CommandBase : ISequenceCommand
{
    public virtual string DebugName => GetType().Name;
    
    // Ignore: drop trivial VFX/SFX/shakes on skip.
    // ExecuteEvenIfSkipping: must still run (text/log/signals).
    protected virtual SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
    
    // If true, the StepGateRunner waits for this command to finish before moving on.
    // If false, it runs in the background (fire-and-forget) and should be tracked via SequencePlayer.
    public virtual bool WaitForCompletion => true;
    protected bool IsCancelled(CommandRunScope scope) => scope.Token.IsCancellationRequested;

    public IEnumerator Execute(CommandRunScope scope)
    {
        if (scope == null) yield break;
        if (scope.Token.IsCancellationRequested) yield break;

        if (scope.IsSkipping)
        {
            switch (SkipPolicy)
            {
                case SkipPolicy.Ignore:
                    yield break;

                case SkipPolicy.CompleteImmediately:
                    try { OnSkip(scope); }
                    catch (System.Exception e) { Debug.LogException(e); }
                    yield break;

                case SkipPolicy.ExecuteEvenIfSkipping:
                    break;
            }
        }

        IEnumerator inner = null;
        try { inner = ExecuteInner(scope); }
        catch (System.Exception e) { Debug.LogException(e); yield break; }

        if (inner != null) yield return inner;
    }

    protected abstract IEnumerator ExecuteInner(CommandRunScope scope);

    protected virtual void OnSkip(CommandRunScope scope) { }
    
    protected IEnumerator Wait(CommandRunScope scope, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (scope.Token.IsCancellationRequested) yield break;

            if (scope.IsSkipping)
            {
                if (SkipPolicy == SkipPolicy.CompleteImmediately)
                {
                    try { OnSkip(scope); }
                    catch (System.Exception e) { Debug.LogException(e); }
                }
                
                yield break;
            }

            elapsed += Time.unscaledDeltaTime * scope.TimeScale;
            
            yield return null;
        }
    }
}

