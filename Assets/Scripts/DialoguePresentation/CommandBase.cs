using System.Collections;
using System.Threading;
using UnityEngine;

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
    /// <summary>이 커맨드가 끝날 때까지 큐가 기다릴지 여부</summary>
    public virtual bool WaitForCompletion => true;

    /// <summary>디버그용 이름 (인스펙터/로그에서 사용)</summary>
    public virtual string DebugName => GetType().Name;

    /// <summary>스킵 모드일 때도 이 커맨드를 꼭 실행해야 하는지</summary>
    protected virtual bool ForceExecuteWhenSkipping => false;

    /// <summary>기본 실행 진입점 (템플릿 메서드 패턴)</summary>
    public IEnumerator Execute(CommandContext ctx)
    {
        // 이미 취소됐으면 그냥 종료
        if (ctx.Token.IsCancellationRequested)
            yield break;

        // 스킵 모드인데, 스킵 가능 커맨드면 즉시 스킵 로직 태운 뒤 종료
        if (ctx.IsSkipping && !ForceExecuteWhenSkipping)
        {
            OnSkip(ctx);
            yield break;
        }

        // 실제 연출 로직
        yield return ExecuteInner(ctx);
    }

    /// <summary>
    /// 자식이 구현해야 하는 실제 연출 본체
    /// (타이핑, 컷인, 카메라 등등)
    /// </summary>
    protected abstract IEnumerator ExecuteInner(CommandContext ctx);

    /// <summary>
    /// 스킵할 때의 동작 (예: 텍스트 즉시 완성, 페이드 바로 완료 등)
    /// 기본은 아무것도 안 함
    /// </summary>
    protected virtual void OnSkip(CommandContext ctx)
    {
        // 필요하면 자식이 override
    }

    /// <summary>
    /// 타임스케일 + 스킵/취소를 고려해서 기다리는 헬퍼
    /// </summary>
    protected IEnumerator Wait(CommandContext ctx, float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float t = 0f;
        while (t < seconds)
        {
            if (ctx.Token.IsCancellationRequested)
                yield break;

            if (ctx.IsSkipping) // 스킵이면 더 이상 기다릴 필요 없음
                yield break;

            t += Time.deltaTime * ctx.TimeScale;
            yield return null;
        }
    }
}
