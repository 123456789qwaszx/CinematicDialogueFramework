using System.Collections;
using System.Threading;
using UnityEngine;




public abstract class CommandBase : ISequenceCommand
{
    public virtual string DebugName => GetType().Name;
    // 스킵이면 그냥 안 해도 되는 단순 SFX, 장식 파티클, 작은 흔들림 등은 Ignore로 override
    // 대사 출력/로그/시그널 발행 같은 커맨드는 ExecuteEvenIfSkipping으로 override
    protected virtual SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
    
    public virtual bool WaitForCompletion => true;
    protected bool IsCancelled(NodePlayScope scope) => scope.Token.IsCancellationRequested;

    public IEnumerator Execute(NodePlayScope scope)
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

    protected abstract IEnumerator ExecuteInner(NodePlayScope scope);

    protected virtual void OnSkip(NodePlayScope scope) { }
    
    protected IEnumerator Wait(NodePlayScope scope, float seconds)
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

