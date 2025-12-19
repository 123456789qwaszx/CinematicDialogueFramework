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
    private NodePlayScope _api;     // ✅ 명확히 api로
    private SequencePlayer _player;

    private CancellationTokenSource _cts;
    private Coroutine _mainRoutine;

    private INodeCommandFactory _commandFactory;

    private int _runId = 0;
    private bool _isStopping = false;

    private void Awake()
    {
        _services = new CommandService(commandServiceConfig); // (Ticket05 버전이면 logContext 전달)
        _player = new SequencePlayer(this);
        _commandFactory = new DefaultNodeCommandFactory();
    }

    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();

    public void Play(DialogueNodeSpec node, DialogueContext state, DialogueLine fallbackLine = null)
    {
        if (node == null || state == null) return;

        Stop();
        _runId++;

        var commands = BuildCommandsFrom(node);

        if ((commands == null || commands.Count == 0) && fallbackLine != null)
            commands = new List<ISequenceCommand> { new ShowLineCommand(fallbackLine) };

        if (commands == null || commands.Count == 0)
            return;

        if (logCommands)
            Debug.Log($"[CommandExecutor] Node Play: commands={commands.Count}", this);

        ResetToken();
        _api.Token = _cts.Token;

        int capturedRunId = _runId;
        _mainRoutine = StartCoroutine(RunNode(commands, _api, capturedRunId));
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

            if (_api != null)
            {
                // ✅ API답게: Busy 제어는 메서드로
                _api.SetNodeBusy(false);
                _api.Token = CancellationToken.None;
            }

            DisposeToken();
        }
        finally
        {
            _isStopping = false;
        }
    }

    private IEnumerator RunNode(List<ISequenceCommand> commands, NodePlayScope api, int runId)
    {
        if (commands == null || api == null) yield break;
        if (runId != _runId) yield break;

        api.SetNodeBusy(true);

        if (logCommands)
            Debug.Log($"[CommandExecutor] Node Begin (runId={runId})", this);

        try
        {
            yield return _player.PlayCommands(
                commands,
                api,
                isValid: () => runId == _runId && !_isStopping,
                log: logCommands);
        }
        finally
        {
            if (runId == _runId && api != null)
            {
                api.SetNodeBusy(false);
                api.Token = CancellationToken.None;
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
