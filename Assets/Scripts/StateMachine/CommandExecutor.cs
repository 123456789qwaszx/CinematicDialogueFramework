using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public sealed class CommandExecutor : MonoBehaviour, INodeExecutor
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    // ---- Dependencies (set by Initialize) ----
    private SequencePlayer _sequencePlayer;
    private INodeCommandFactory _commandFactory;

    // ---- Runtime state: execution ----
    private CancellationTokenSource _cts;
    private Coroutine _mainRoutine;
    private NodePlayScope _activeScope;

    // ---- Runtime state: control flags ----
    private int _runId;
    private bool _isStopInProgress;
    private bool _isInitialized;

    public void Initialize(SequencePlayer sequencePlayer, INodeCommandFactory commandFactory)
    {
        _sequencePlayer = sequencePlayer;
        _commandFactory = commandFactory;
        _isInitialized = true;
    }

    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();
    
    
    public void PlayStep(DialogueNodeSpec node, int stepIndex, NodePlayScope scope, DialogueLine fallbackLine = null)
    {
        if (!_isInitialized)
            return;
        if (node == null || scope == null)
            return;

        Stop();
        List<ISequenceCommand> commands = BuildCommandsFromStep(node, stepIndex);
        if (commands == null || commands.Count == 0)
        {
            if (fallbackLine != null)
            {
                commands = new List<ISequenceCommand> { new ShowLineCommand(fallbackLine) };
            }
            else
            {
                Log($"Step skipped: stepIndex={stepIndex} (no commands)");
                return;
            }
        }
        
        _activeScope = scope;
        
        ResetToken();
        _activeScope.Token = _cts.Token;
        
        Log($"Step Play: stepIndex={stepIndex}, commands={commands.Count}");
        _mainRoutine = StartCoroutine(RunNode(commands, _activeScope, _runId));
    }
    
    private IEnumerator RunNode(List<ISequenceCommand> commands, NodePlayScope scope, int runId)
    {
        if (runId != _runId)
        {
            Log($"RunNode exited early: stale runId={runId}, current={_runId}");
            yield break;
        }

        scope.SetNodeBusy(true);
        
        Log($"Node Begin (runId={runId})");
        try
        {
            yield return _sequencePlayer.PlayCommands(
                commands,
                scope,
                runId: runId,
                isValid: () => runId == _runId,
                trace: msg => Log(msg)
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

                Log($"Node End (runId={runId})");
            }
        }
    }
    
    public void Stop()
    {
        if (_isStopInProgress)
            return;
        
        _isStopInProgress = true;

        try
        {
            // Bump run generation so all in-flight routines from the previous session become invalid.
            _runId++;
            CancelAndDisposeToken();

            if (_mainRoutine != null)
            {
                StopCoroutine(_mainRoutine);
                _mainRoutine = null;
            }

            _sequencePlayer?.Stop();

            if (_activeScope != null)
            {
                _activeScope.SetNodeBusy(false);
                _activeScope.Token = CancellationToken.None;
                _activeScope = null;
            }

            Log("Stop() called");
        }
        finally
        {
            _isStopInProgress = false;
        }
    }
    

    private List<ISequenceCommand> BuildCommandsFromStep(DialogueNodeSpec node, int stepIndex)
    {
        var list = new List<ISequenceCommand>();

        if (node.steps == null || node.steps.Count == 0)
        {
            Log($"Node Empty (node={node})");
            return list;
        }
        if (stepIndex < 0 || stepIndex >= node.steps.Count)
        {
            Log($"Invalid stepIndex: {stepIndex} (steps={node.steps.Count})");
            return list;
        }

        DialogueStepSpec step = node.steps[stepIndex];
        if (step == null || step.commands == null || step.commands.Count == 0)
        {
            Log($"Step Empty (step={step})");
            return list;
        }

        foreach (NodeCommandSpec spec in step.commands)
        {
            if (!_commandFactory.TryCreate(spec, out ISequenceCommand cmd))
            {
                Log($"failed to create command (spec={spec})");
                continue;
            }
            
            list.Add(cmd);
        }

        return list;
    }
    
    private void ResetToken()
    { // 새 실행용 토큰 생성만 담당
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }
    
    private void CancelAndDisposeToken()
    {
        if (_cts == null)
            return;

        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch (ObjectDisposedException) { }

        _cts.Dispose();
        _cts = null;
    }
    
    
    private void Log(string msg)
    {
        if (!enableDebugLog) return;
        Debug.Log($"[CommandExecutor] {msg}", this);
    }
}
