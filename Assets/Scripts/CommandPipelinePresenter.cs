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

        _services       = new CommandService(commandServiceConfig);
        _player         = new SequencePlayer();
        _commandContext = new CommandContext(_services)
        {
            TimeScale  = 1f,
            IsSkipping = false,
            IsAutoMode = false,
            Token      = CancellationToken.None
        };
    }

    private void OnDestroy()
    {
        DisposeToken();
    }

    /// <summary>
    /// 상태머신 쪽 Context 값을 CommandContext로 넘기고 싶을 때 사용.
    /// (지금은 선택사항. 나중에 Auto/Skip/TimeScale 연동할 때 쓰면 됨)
    /// </summary>
    public void SyncFrom(DialogueContext ctx)
    {
        if (_commandContext == null) return;

        _commandContext.IsAutoMode = ctx.IsAutoMode;
        _commandContext.IsSkipping = ctx.IsSkipping;
        _commandContext.TimeScale  = ctx.TimeScale <= 0f ? 1f : ctx.TimeScale;
    }

    // ---- IDialoguePresenter 구현부 ----

    public void Present(NodeViewModel vm)
    {
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
        return new DialogueLine
        {
            speakerId  = vm.SpeakerId,
            expression = Expression.Default,
            text       = vm.Text,
            position   = DialoguePosition.Left
        };
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
