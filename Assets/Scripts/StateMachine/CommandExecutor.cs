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
    [SerializeField] private bool logTraceStreaming = false;
    [SerializeField] private bool logTraceDumpOnNodeEnd = true;

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

    /// <summary>
    /// Compatibility: 기존 호출부가 아직 "노드 단위"로 Play를 부르는 경우를 위해 유지.
    /// Step 구조로 바뀐 이후에는 보통 PlayStep(node, stepIndex, ...)를 쓰는 게 맞다.
    /// </summary>
    public void Play(DialogueNodeSpec node, NodePlayScope scope, DialogueLine fallbackLine = null)
    {
        // 기본 동작을 "Step 0 재생"으로 두면, 최소한 이전 흐름이 완전히 죽진 않음.
        // (올바른 흐름은 호출부에서 현재 stepIndex를 넘겨 PlayStep을 호출하는 것)
        PlayStep(node, stepIndex: 0, scope: scope, fallbackLine: fallbackLine);
    }

    /// <summary>
    /// ✅ Step 기반 재생: 현재 step의 commands만 실행한다.
    /// </summary>
    public void PlayStep(DialogueNodeSpec node, int stepIndex, NodePlayScope scope, DialogueLine fallbackLine = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[CommandExecutor] Play called before Initialize().", this);
            return;
        }

        if (node == null || scope == null) return;

        Stop();
        ClearTrace();

        var commands = BuildCommandsFromStep(node, stepIndex);

        if ((commands == null || commands.Count == 0) && fallbackLine != null)
            commands = new List<ISequenceCommand> { new ShowLineCommand(fallbackLine) };

        if (commands == null || commands.Count == 0)
        {
            Trace($"Step Play skipped: stepIndex={stepIndex} (no commands)");
            return;
        }

        Trace($"Step Play: stepIndex={stepIndex}, commands={commands.Count}");

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
                runId: runId,
                isValid: () => runId == _runId && !_isStopping,
                trace: Trace
            );
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

    private List<ISequenceCommand> BuildCommandsFromStep(DialogueNodeSpec node, int stepIndex)
    {
        var list = new List<ISequenceCommand>();

        if (node == null || node.steps == null || node.steps.Count == 0)
            return list;

        if (_commandFactory == null)
        {
            Debug.LogError("[CommandExecutor] CommandFactory is null. Did you call Initialize()?", this);
            return list;
        }

        if (stepIndex < 0 || stepIndex >= node.steps.Count)
        {
            Trace($"Invalid stepIndex: {stepIndex} (steps={node.steps.Count})");
            return list;
        }

        var step = node.steps[stepIndex];
        if (step == null || step.commands == null || step.commands.Count == 0)
            return list;

        foreach (var spec in step.commands)
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
