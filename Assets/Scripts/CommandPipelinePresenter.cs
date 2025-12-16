using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 상태머신 Dialogue 코어와
/// 연출용 Command 파이프라인을 이어주는 Presenter.
///
/// - DialogueSession 에서는 NodeViewModel 단위로 Present(vm)을 호출
/// - 여기서는 vm을 DialogueLine으로 변환해서 ShowLineCommand 등 Command로 실행
/// - 실제 화면 표현은 CommandServiceConfig 에서 주입된 DialogueViewAsset이 담당
/// - ✅ 실행 모드 컨텍스트(DilogueContext)는 상태머신이 소유하고,
///   CommandContext는 그걸 래핑해서 사용한다.
/// </summary>
public sealed class CommandPipelinePresenter : MonoBehaviour, IDialoguePresenter
{
    [Header("Command Pipeline Config")]
    [SerializeField] private CommandServiceConfig commandServiceConfig;

    private CommandService _services;
    private SequencePlayer _player;
    private CommandContext _commandContext;

    private CancellationTokenSource _cts;
    private Coroutine _currentRoutine;

    private void Awake()
    {
        if (commandServiceConfig == null)
        {
            Debug.LogWarning(
                "[CommandPipelinePresenter] CommandServiceConfig is null. " +
                "Commands that use services (ShowLine, ShakeCamera 등)는 동작하지 않을 수 있습니다.");
        }

        _services = new CommandService(commandServiceConfig);
        _player   = new SequencePlayer();

        // ✅ _commandContext는 아직 생성하지 않는다.
        // 실제 공유 DialogueContext가 넘어올 때 SyncFrom에서 생성한다.
    }

    private void OnDestroy()
    {
        DisposeToken();
    }

    /// <summary>
    /// 상태머신 쪽 Context를 Command 파이프라인과 공유하기 위한 메서드.
    /// - 이전에는 값을 복사(Sync)만 했지만,
    /// - 이제는 동일한 DialogueContext 인스턴스를 붙잡기만 한다.
    /// </summary>
    public void SyncFrom(DialogueContext ctx)
    {
        if (ctx == null)
            return;

        // 처음 호출되면 CommandContext를 생성하면서 공유 Context를 래핑
        if (_commandContext == null)
        {
            _commandContext = new CommandContext(_services, ctx)
            {
                Token = CancellationToken.None
            };

            // TimeScale이 0 이하로 내려가는 걸 방지
            if (ctx.TimeScale <= 0f)
                ctx.TimeScale = 1f;

            return;
        }

        // 이미 있고, 다른 Context 인스턴스를 받았다면 새로 래핑
        if (!ReferenceEquals(_commandContext.DialogueContext, ctx))
        {
            var prevToken = _commandContext.Token;

            _commandContext = new CommandContext(_services, ctx)
            {
                Token = prevToken
            };

            if (ctx.TimeScale <= 0f)
                ctx.TimeScale = 1f;
        }
    }

    // ---- IDialoguePresenter 구현부 ----

    public void Present(NodeViewModel vm)
    {
        if (_commandContext == null)
        {
            Debug.LogWarning("[CommandPipelinePresenter] CommandContext is null. Did you call SyncFrom(...)?");
            return;
        }

        // 이전 재생 중이던 커맨드가 있으면 정리
        StopCurrent();

        // NodeViewModel -> DialogueLine 변환
        DialogueLine line = BuildDialogueLineFrom(vm);

        // 필요한 커맨드들을 조합 (1주차는 일단 한 줄 출력만)
        var commands = new List<ISequenceCommand>
        {
            new ShowLineCommand(line)
            // TODO: 나중에 표정/포지션/상황에 따라 ShakeCameraCommand 등 추가
        };

        // CancellationToken 갱신
        ResetToken();

        // CommandContext에 Token 적용
        _commandContext.Token = _cts.Token;

        // Command 시퀀스 실행
        _currentRoutine = StartCoroutine(_player.PlayCommands(commands, _commandContext));
    }

    public void PresentSystemMessage(string message)
    {
        if (_commandContext == null)
        {
            Debug.LogWarning("[CommandPipelinePresenter] CommandContext is null. Did you call SyncFrom(...)?");
            return;
        }

        StopCurrent();

        // 간단하게 "System" 스피커로 한 줄 뿌려주는 형태로 처리
        DialogueLine line = new DialogueLine
        {
            speakerId  = "System",
            expression = Expression.Default,
            text       = message,
            position   = DialoguePosition.Center
        };

        var commands = new List<ISequenceCommand>
        {
            new ShowLineCommand(line)
        };

        ResetToken();
        _commandContext.Token = _cts.Token;

        _currentRoutine = StartCoroutine(_player.PlayCommands(commands, _commandContext));
    }

    public void Clear()
    {
        // 진행 중인 커맨드 정리
        StopCurrent();

        // 뷰를 완전히 비우고 싶다면, 빈 라인을 immediate로 한 번 뿌려버리는 것도 가능
        try
        {
            var empty = new DialogueLine
            {
                speakerId  = "",
                expression = Expression.Default,
                text       = "",
                position   = DialoguePosition.Center
            };

            _services?.ShowLineImmediate(empty);
        }
        catch
        {
            // 뷰가 아직 세팅 안 되어 있다면 조용히 무시
        }
    }

    // ---- 내부 헬퍼 ----

    private DialogueLine BuildDialogueLineFrom(NodeViewModel vm)
    {
        // 상태머신 스펙에는 Expression/Position 정보가 없으니
        // 1주차는 일단 기본값으로 채우고,
        // 나중에 확장할 때 Branch/Variant 등을 보고 매핑할 수 있음.
        return vm.Line;
    }

    private void StopCurrent()
    {
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            _currentRoutine = null;
        }

        DisposeToken();
    }

    private void ResetToken()
    {
        DisposeToken();
        _cts = new CancellationTokenSource();
    }

    private void DisposeToken()
    {
        if (_cts == null) return;

        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _cts.Dispose();
        _cts = null;
    }
}
