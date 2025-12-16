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
///
/// ✅ 중요한 규칙:
/// - 이 클래스는 "이 노드의 연출이 재생 중인지"만 DialogueContext.IsNodeBusy로 알려준다.
/// - 다음 진행(입력/딜레이/신호 대기)은 전부 GateRunner가 GateToken으로만 결정.
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
        StopCurrent();
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

        if (commands.Count == 0 && vm.PrimaryLine != null)
        {
            // 아무 커맨드도 없으면, PrimaryLine이 있다면 fallback으로 ShowLine 하나라도 실행
            commands.Add(new ShowLineCommand(vm.PrimaryLine));
        }

        if (commands.Count == 0)
        {
            // 진짜로 아무것도 없으면 busy를 켰다가 끌 이유도 없음
            return;
        }

        ResetToken();
        _commandContext.Token = _cts.Token;

        _currentRoutine = StartCoroutine(RunNodeCommands(commands));
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

        _currentRoutine = StartCoroutine(RunNodeCommands(commands));
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

    /// <summary>
    /// 한 노드의 커맨드 묶음을 재생하는 실제 코루틴.
    /// - 시작 시 IsNodeBusy = true
    /// - 끝나면 IsNodeBusy = false
    /// </summary>
    private IEnumerator RunNodeCommands(List<ISequenceCommand> commands)
    {
        var ctx = _commandContext;
        if (ctx == null)
            yield break;

        // ✅ 노드 연출 시작: busy ON
        ctx.IsNodeBusy = true;

        yield return _player.PlayCommands(commands, ctx);

        // ✅ 노드 연출 완료: busy OFF
        ctx.IsNodeBusy = false;
    }

    private List<ISequenceCommand> BuildCommandsFrom(NodeViewModel vm)
    {
        var list  = new List<ISequenceCommand>();
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

                // TODO: PlaySE, BGM, CutIn 등 확장 시 여기서 매핑
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

        // 토큰 취소
        DisposeToken();

        // 혹시 남아있을 수 있는 busy 플래그 안전하게 OFF
        if (_commandContext != null)
        {
            _commandContext.IsNodeBusy = false;
        }
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
