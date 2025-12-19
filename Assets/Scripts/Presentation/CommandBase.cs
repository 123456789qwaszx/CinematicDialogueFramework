using System.Collections;
using System.Threading;
using UnityEngine;

public enum SkipPolicy
{
    /// <summary>스킵 중이면 이 커맨드는 실행하지 않는다(기본).</summary>
    Ignore = 0,

    /// <summary>스킵 중이면 즉시 완료 처리(OnSkip 호출).</summary>
    CompleteImmediately = 1,

    /// <summary>스킵 중이어도 그대로 실행한다(예: 중요한 시스템 연출/상태 반영).</summary>
    ExecuteEvenIfSkipping = 2
}


public interface ISequenceCommand
{
    bool WaitForCompletion { get; }

    IEnumerator Execute(CommandContext ctx);
}

public class CommandContext
{
    private readonly CommandService _services;
    private readonly DialogueContext _dialogueContext;

    /// <summary>
    /// 공용 실행 컨텍스트(스킵/오토/타임스케일/노드 busy)를 래핑한다.
    /// </summary>
    public DialogueContext DialogueContext => _dialogueContext;

    public CommandContext(CommandService services)
        : this(services, new DialogueContext())
    {
    }

    public CommandContext(CommandService services, DialogueContext dialogueContext)
    {
        _services = services;
        _dialogueContext = dialogueContext ?? new DialogueContext();
    }

    /// <summary>
    /// 스킵 모드: true면 가능한 모든 대기/연출을 즉시 통과하려고 시도
    /// </summary>
    public bool IsSkipping
    {
        get => _dialogueContext != null && _dialogueContext.IsSkipping;
        set
        {
            if (_dialogueContext != null)
                _dialogueContext.IsSkipping = value;
        }
    }

    /// <summary>
    /// 오토 모드: true면 Input 게이트도 일정 딜레이 후 자동 통과
    /// </summary>
    public bool IsAutoMode
    {
        get => _dialogueContext != null && _dialogueContext.IsAutoMode;
        set
        {
            if (_dialogueContext != null)
                _dialogueContext.IsAutoMode = value;
        }
    }

    /// <summary>
    /// 연출용 타임스케일 (0 이하로 내려가면 최소값으로 보정해서 사용)
    /// </summary>
    public float TimeScale
    {
        get => _dialogueContext != null && _dialogueContext.TimeScale > 0f
            ? _dialogueContext.TimeScale
            : 1f;
        set
        {
            if (_dialogueContext != null)
                _dialogueContext.TimeScale = value;
        }
    }

    /// <summary>
    /// 현재 노드의 Command 파이프라인이 재생 중인지 여부.
    /// - Presenter/SequencePlayer에서 제어
    /// </summary>
    public bool IsNodeBusy
    {
        get => _dialogueContext != null && _dialogueContext.IsNodeBusy;
        set
        {
            if (_dialogueContext != null)
                _dialogueContext.IsNodeBusy = value;
        }
    }

    /// <summary>
    /// 커맨드 취소 토큰 (코루틴 실행 중단용)
    /// </summary>
    public CancellationToken Token { get; set; }

    // ==== 서비스 래핑 메서드들 ====

    public void ShakeCamera(float strength, float duration)
    {
        _services?.ShakeCamera(strength, duration);
    }

    public IEnumerator ShowLine(DialogueLine line)
    {
        return _services?.ShowLine(line, this);
    }

    public void ShowLineImmediate(DialogueLine line)
    {
        _services?.ShowLineImmediate(line);
    }
}

public abstract class CommandBase : ISequenceCommand
{
    public virtual bool WaitForCompletion => true;
    public virtual string DebugName => GetType().Name;

    // ✅ Ticket 07: 선언형 스킵 정책
    public virtual SkipPolicy SkipPolicy => SkipPolicy.Ignore;

    public IEnumerator Execute(CommandContext ctx)
    {
        if (ctx == null) yield break;
        if (ctx.Token.IsCancellationRequested) yield break;

        // ✅ Ticket 07: 정책 기반 분기
        if (ctx.IsSkipping)
        {
            switch (SkipPolicy)
            {
                case SkipPolicy.Ignore:
                    yield break;

                case SkipPolicy.CompleteImmediately:
                    try { OnSkip(ctx); }
                    catch (System.Exception e) { UnityEngine.Debug.LogException(e); }
                    yield break;

                case SkipPolicy.ExecuteEvenIfSkipping:
                    // 그대로 실행 진행
                    break;
            }
        }

        IEnumerator inner = null;
        try
        {
            inner = ExecuteInner(ctx);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogException(e);
            yield break;
        }

        if (inner != null)
            yield return inner;
    }

    protected abstract IEnumerator ExecuteInner(CommandContext ctx);

    /// <summary>
    /// SkipPolicy == CompleteImmediately 일 때 호출됨.
    /// </summary>
    protected virtual void OnSkip(CommandContext ctx) { }

    // Wait()는 Ticket 03에서 unscaled로 이미 통일했다고 가정
}

