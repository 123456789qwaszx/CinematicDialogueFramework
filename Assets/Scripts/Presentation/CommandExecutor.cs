using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Executes node-level cinematic commands (Command pipeline) for the dialogue system.
/// - Builds runtime commands from DialogueNodeSpec (authoring specs)
/// - Runs them sequentially using coroutine
/// - Controls DialogueContext.IsNodeBusy (gate progression must wait while busy)
/// - Supports Stop() via cancellation + coroutine stop
/// </summary>
public sealed class CommandExecutor : MonoBehaviour, INodeExecutor
{
    [SerializeField] private CommandServiceConfig commandServiceConfig;

    private CommandService _services;
    private CommandContext _commandContext;

    private CancellationTokenSource _cts;
    private Coroutine _currentRoutine;

    private void Awake()
    {
        _services = new CommandService(commandServiceConfig);
    }

    private void OnDestroy()
    {
        Stop();
    }

    /// <summary>
    /// Keeps CommandContext in sync with the DialogueSession-owned DialogueContext.
    /// </summary>
    public void SyncFrom(DialogueContext ctx)
    {
        if (ctx == null) return;

        if (_commandContext == null)
        {
            _commandContext = new CommandContext(_services, ctx) { Token = CancellationToken.None };
            if (ctx.TimeScale <= 0f) ctx.TimeScale = 1f;
            return;
        }

        if (!ReferenceEquals(_commandContext.DialogueContext, ctx))
        {
            var prevToken = _commandContext.Token;
            _commandContext = new CommandContext(_services, ctx) { Token = prevToken };
            if (ctx.TimeScale <= 0f) ctx.TimeScale = 1f;
        }
    }

    /// <summary>
    /// Starts executing this node's commands.
    /// If there are no commands, optionally runs a fallback ShowLine command.
    /// </summary>
    public void Play(DialogueNodeSpec node, DialogueContext ctx, DialogueLine fallbackLine = null)
    {
        if (node == null) return;

        SyncFrom(ctx);
        if (_commandContext == null) return;

        Stop();

        List<ISequenceCommand> commands = BuildCommandsFrom(node);

        if (commands.Count == 0 && fallbackLine != null)
            commands.Add(new ShowLineCommand(fallbackLine));

        if (commands.Count == 0)
            return;

        ResetToken();
        _commandContext.Token = _cts.Token;

        _currentRoutine = StartCoroutine(RunNodeCommands(commands));
    }

    public void Stop()
    {
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            _currentRoutine = null;
        }

        DisposeToken();

        // Safety: never leave Busy ON after a forced stop.
        if (_commandContext != null)
            _commandContext.IsNodeBusy = false;
    }

    private IEnumerator RunNodeCommands(List<ISequenceCommand> commands)
    {
        var ctx = _commandContext;
        if (ctx == null) yield break;

        ctx.IsNodeBusy = true;

        // Minimal inlined "SequencePlayer"
        for (int i = 0; i < commands.Count; i++)
        {
            if (ctx.Token.IsCancellationRequested)
                break;

            ISequenceCommand cmd = commands[i];
            if (cmd == null) continue;

            IEnumerator routine = null;
            try
            {
                routine = cmd.Execute(ctx);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // 정책: 커맨드 하나가 터져도 다음으로 진행할지/중단할지는 취향
                // 여기서는 "계속 진행"으로 둠.
            }

            if (cmd.WaitForCompletion && routine != null)
            {
                while (!ctx.Token.IsCancellationRequested && routine.MoveNext())
                    yield return routine.Current;
            }
        }

        ctx.IsNodeBusy = false;
    }

    private static List<ISequenceCommand> BuildCommandsFrom(DialogueNodeSpec node)
    {
        var list = new List<ISequenceCommand>();

        List<NodeCommandSpec> specs = node.commands;
        if (specs == null || specs.Count == 0)
            return list;

        for (int i = 0; i < specs.Count; i++)
        {
            NodeCommandSpec spec = specs[i];
            if (spec == null) continue;

            switch (spec.kind)
            {
                case NodeCommandKind.ShowLine:
                    if (spec.line != null)
                        list.Add(new ShowLineCommand(spec.line));
                    break;

                case NodeCommandKind.ShakeCamera:
                    list.Add(new ShakeCameraCommand
                    {
                        strength = spec.shakeStrength,
                        duration = spec.shakeDuration
                    });
                    break;

                // TODO: 확장 커맨드들 매핑 (BGM/SE/CutIn/Portrait/Transition...)
            }
        }

        return list;
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
        catch (ObjectDisposedException) { }

        _cts.Dispose();
        _cts = null;
    }
}
