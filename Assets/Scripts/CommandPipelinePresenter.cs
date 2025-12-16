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
/// - 여기서는 vm.CommandSpecs를 기반으로 ISequenceCommand 리스트를 만들어 실행
/// - 실제 화면 표현은 CommandServiceConfig 에서 주입된 DialogueViewAsset이 담당
/// - 실행 모드 컨텍스트(DialogueContext)는 상태머신이 소유, CommandContext는 래핑
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
        // _commandContext는 실제 DialogueContext가 SyncFrom으로 넘어올 때 생성
    }

    private void OnDestroy()
    {
        DisposeToken();
    }

    /// <summary>
    /// 상태머신 쪽 DialogueContext를 Command 파이프라인과 공유.
    /// </summary>
    public void SyncFrom(DialogueContext ctx)
    {
        if (ctx == null)
            return;

        if (_commandContext == null)
        {
            _commandContext = new CommandContext(_services, ctx)
            {
                Token = CancellationToken.None
            };

            if (ctx.TimeScale <= 0f)
                ctx.TimeScale = 1f;

            return;
        }

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

        StopCurrent();

        // NodeViewModel.CommandSpecs -> ISequenceCommand 리스트
        List<ISequenceCommand> commands = BuildCommandsFrom(vm);

        if (commands.Count == 0)
        {
            // 아무 커맨드도 없으면, PrimaryLine이 있다면 fallback으로 ShowLine 하나라도 실행
            if (vm.PrimaryLine != null)
                commands.Add(new ShowLineCommand(vm.PrimaryLine));
        }

        ResetToken();
        _commandContext.Token = _cts.Token;

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
        StopCurrent();

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

    private List<ISequenceCommand> BuildCommandsFrom(NodeViewModel vm)
    {
        var list = new List<ISequenceCommand>();

        var specs = vm.CommandSpecs;
        if (specs == null || specs.Count == 0)
            return list;

        for (int i = 0; i < specs.Count; i++)
        {
            NodeCommandSpec spec = specs[i];
            if (spec == null) continue;

            switch (spec.kind)
            {
                case NodeCommandKind.ShowLine:
                {
                    if (spec.line != null)
                        list.Add(new ShowLineCommand(spec.line));
                    break;
                }

                case NodeCommandKind.ShakeCamera:
                {
                    list.Add(new ShakeCameraCommand
                    {
                        strength = spec.shakeStrength,
                        duration = spec.shakeDuration
                    });
                    break;
                }

                // TODO: 나중에 PlaySE, BGM, CutIn 등 추가하면 여기서 매핑
            }
        }

        return list;
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
