using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class CommandExecutor : MonoBehaviour, INodeExecutor
{
    [Header("Trace")]
    [SerializeField] private bool enableTrace = true;

    // 1) Trace가 추가될 때마다 Debug.Log로 스트리밍 출력
    [SerializeField] private bool logTraceStreaming = false;

    // 2) 노드 종료/Stop 시점에 전체 Trace를 한번에 덤프
    [SerializeField] private bool logTraceDumpOnNodeEnd = true;

    // Inspector에서 확인용(선택)
    [SerializeField, TextArea(3, 20)] private string tracePreview;

    private readonly StringBuilder _trace = new StringBuilder(4096);
    private const int MaxTraceChars = 20000;

    private SequencePlayer _player;
    private INodeCommandFactory _commandFactory;

    private CancellationTokenSource _cts;
    private Coroutine _mainRoutine;

    private NodePlayScope _activeScope;

    private int _runId = 0;
    private bool _isStopping = false;
    private bool _isInitialized = false;

    public void Initialize(SequencePlayer player, INodeCommandFactory commandFactory)
    {
        _player = player;
        _commandFactory = commandFactory;
        _isInitialized = true;
    }

    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();

    public void Play(DialogueNodeSpec node, NodePlayScope scope, DialogueLine fallbackLine = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[CommandExecutor] Play called before Initialize().", this);
            return;
        }
        if (node == null || scope == null) return;

        Stop();
        ClearTrace(); // ✅ 새 노드 시작할 때 trace 초기화(원치 않으면 지워도 됨)

        var commands = BuildCommandsFrom(node);

        if ((commands == null || commands.Count == 0) && fallbackLine != null)
            commands = new List<ISequenceCommand> { new ShowLineCommand(fallbackLine) };

        if (commands == null || commands.Count == 0)
            return;

        Trace($"Node Play: commands={commands.Count}");

        _activeScope = scope;

        ResetToken();
        _activeScope.Token = _cts.Token;

        int capturedRunId = _runId;
        _mainRoutine = StartCoroutine(RunNode(commands, _activeScope, capturedRunId));
    }

    public void Stop()
    {
        if (_isStopping) return;
        _isStopping = true;

        try
        {
            _runId++;
            CancelTokenOnly();

            if (_mainRoutine != null)
            {
                StopCoroutine(_mainRoutine);
                _mainRoutine = null;
            }

            _player?.Stop();

            if (_activeScope != null)
            {
                _activeScope.SetNodeBusy(false);
                _activeScope.Token = CancellationToken.None;
                _activeScope = null;
            }

            DisposeToken();

            Trace("Stop()");
            if (logTraceDumpOnNodeEnd) DumpTraceToConsole("[CommandExecutor] Trace dump (Stop)");
        }
        finally
        {
            _isStopping = false;
        }
    }

    private IEnumerator RunNode(List<ISequenceCommand> commands, NodePlayScope scope, int runId)
    {
        if (commands == null || scope == null) yield break;
        if (runId != _runId) yield break;

        scope.SetNodeBusy(true);
        Trace($"Node Begin (runId={runId})");

        try
        {
            yield return _player.PlayCommands(
                commands,
                scope,
                isValid: () => runId == _runId && !_isStopping,
                trace: Trace);
        }
        finally
        {
            if (runId == _runId && scope != null)
            {
                scope.SetNodeBusy(false);
                scope.Token = CancellationToken.None;

                if (ReferenceEquals(_activeScope, scope))
                    _activeScope = null;

                _mainRoutine = null;

                Trace($"Node End (runId={runId})");
                DisposeToken();

                if (logTraceDumpOnNodeEnd)
                    DumpTraceToConsole("[CommandExecutor] Trace dump (Node End)");
            }
        }
    }

    private List<ISequenceCommand> BuildCommandsFrom(DialogueNodeSpec node)
    {
        var list = new List<ISequenceCommand>();
        if (node == null || node.commands == null) return list;

        if (_commandFactory == null)
        {
            Debug.LogError("[CommandExecutor] CommandFactory is null. Did you call Initialize()?", this);
            return list;
        }

        foreach (var spec in node.commands)
        {
            if (spec == null) continue;

            if (_commandFactory.TryCreate(spec, out var cmd) && cmd != null)
                list.Add(cmd);
            else
                Trace($"Unsupported command kind: {spec.kind}");
        }

        return list;
    }

    // ----------------------
    // Trace helpers
    // ----------------------

    private void Trace(string msg)
    {
        if (!enableTrace) return;

        if (_trace.Length > MaxTraceChars)
            _trace.Remove(0, _trace.Length - (MaxTraceChars / 2));

        string line = $"[{Time.frameCount}] {msg}";

        _trace.AppendLine(line);
        tracePreview = _trace.ToString();

        if (logTraceStreaming)
            Debug.Log(line, this);
    }

    private void DumpTraceToConsole(string header)
    {
        if (!enableTrace) return;

        // Debug.Log는 길이 제한/가독성 문제가 있으니 헤더와 함께 한 번만 출력
        Debug.Log($"{header}\n{_trace}", this);
    }

    public void ClearTrace()
    {
        _trace.Clear();
        tracePreview = "";
    }

    // ----------------------
    // Token lifecycle
    // ----------------------

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
