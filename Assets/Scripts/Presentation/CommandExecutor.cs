using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public sealed class CommandExecutor : MonoBehaviour, INodeExecutor
{
    [Header("Config")]
    [SerializeField] private CommandServiceConfig commandServiceConfig;

    [Header("Debug")]
    [SerializeField] private bool logCommands = false;

    private CommandService _services;
    private CommandContext _commandContext;
    private SequencePlayer _player;

    private CancellationTokenSource _cts;
    private Coroutine _mainRoutine;

    private INodeCommandFactory _commandFactory;

    private int _runId = 0;
    private bool _isStopping = false;
    [SerializeField] private bool logNodeBoundary = false;
    private void Awake()
    {

        _services = new CommandService(commandServiceConfig);
        _player = new SequencePlayer(this);
        _commandFactory = new DefaultNodeCommandFactory();
    }

    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();

    public void Play(DialogueNodeSpec node, DialogueContext ctx, DialogueLine fallbackLine = null)
    {
        if (node == null || ctx == null) return;

        Stop();          // 완전 정지(이전 run 잔여 제거)
        _runId++;         // 새로운 run 시작

        if (_commandContext == null || !ReferenceEquals(_commandContext.DialogueContext, ctx))
            _commandContext = new CommandContext(_services, ctx);

        var commands = BuildCommandsFrom(node);
        if (logCommands)
        {
            Debug.Log($"[CommandExecutor] Node Play: commands={commands.Count}", this);
        }
        if ((commands == null || commands.Count == 0) && fallbackLine != null)
            commands = new List<ISequenceCommand> { new ShowLineCommand(fallbackLine) };

        if (commands == null || commands.Count == 0)
            return;

        ResetToken();
        _commandContext.Token = _cts.Token;

        int capturedRunId = _runId;
        _mainRoutine = StartCoroutine(RunNode(commands, _commandContext, capturedRunId));
    }

    public void Stop()
    {
        if (_isStopping) return;
        _isStopping = true;

        try
        {
            _runId++; // invalidate stale routines

            CancelTokenOnly();

            if (_mainRoutine != null)
            {
                StopCoroutine(_mainRoutine);
                _mainRoutine = null;
            }

            _player?.Stop();

            if (_commandContext != null)
            {
                _commandContext.IsNodeBusy = false;
                _commandContext.Token = CancellationToken.None;
            }

            DisposeToken();
        }
        finally
        {
            _isStopping = false;
        }
    }

    private IEnumerator RunNode(List<ISequenceCommand> commands, CommandContext ctx, int runId)
    {
        if (commands == null || ctx == null) yield break;
        if (runId != _runId) yield break;

        ctx.IsNodeBusy = true;

        if (logCommands)
            Debug.Log($"[CommandExecutor] Node Begin (runId={runId})", this);

        try
        {
            yield return _player.PlayCommands(
                commands,
                ctx,
                isValid: () => runId == _runId && !_isStopping,
                log: logCommands);
        }
        finally
        {
            if (runId == _runId && ctx != null)
            {
                ctx.IsNodeBusy = false;
                ctx.Token = System.Threading.CancellationToken.None;
                _mainRoutine = null;

                if (logCommands)
                    Debug.Log($"[CommandExecutor] Node End (runId={runId})", this);

                DisposeToken();
            }
        }
    }

    private List<ISequenceCommand> BuildCommandsFrom(DialogueNodeSpec node)
    {
        var list = new List<ISequenceCommand>();
        if (node == null || node.commands == null) return list;

        _commandFactory ??= new DefaultNodeCommandFactory();

        foreach (var spec in node.commands)
        {
            if (spec == null) continue;

            if (_commandFactory.TryCreate(spec, out var cmd) && cmd != null)
                list.Add(cmd);
            else if (logCommands)
                Debug.LogWarning($"[CommandExecutor] Unsupported command kind: {spec.kind}", this);
        }

        return list;
    }

    private void ResetToken()
    {
        DisposeToken();
        _cts = new CancellationTokenSource();
    }

    private void CancelTokenOnly()
    {
        if (_cts == null) return;

        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch (ObjectDisposedException) { }
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
