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
    private CommandRunScope _activeScope;

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

    private void OnDisable() => Stop(CleanupPolicy.Cancel);
    private void OnDestroy() => Stop(CleanupPolicy.Cancel);
    
    
    public void PlayStep(NodeSpec node, int stepIndex, CommandRunScope scope, DialogueLine fallbackLine = null)
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
                var fallbackSpec = new ShowLineCommandSpec
                {
                    line = fallbackLine,
                    // 필요하면 node 쪽에서 기본 screenId / widgetId를 꺼내서 세팅
                    // screenId = node.defaultScreenId,
                    // widgetId = node.defaultWidgetId,
                };

                if (_commandFactory.TryCreate(fallbackSpec, out ISequenceCommand fallbackCommand)
                    && fallbackCommand != null)
                {
                    commands = new List<ISequenceCommand> { fallbackCommand };
                }
                else
                {
                    Log("Failed to create fallback ShowLine command");
                    return;
                }
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
    
    private IEnumerator RunNode(List<ISequenceCommand> commands, CommandRunScope scope, int runId)
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
        CleanupPolicy policy = DecideCleanupPolicy(_activeScope);
        Stop(policy);
    }

    public void Stop(CleanupPolicy policy)
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
                _activeScope.Cleanup(policy);
                _activeScope.SetNodeBusy(false);
                _activeScope.Token = CancellationToken.None;
                _activeScope = null;
            }

            Log($"Stop(policy={policy})");
        }
        finally
        {
            _isStopInProgress = false;
        }
    }
    

    private List<ISequenceCommand> BuildCommandsFromStep(NodeSpec node, int stepIndex)
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

        StepSpec step = node.steps[stepIndex];
        if (step == null || step.commands == null || step.commands.Count == 0)
        {
            Log($"Step Empty (step={step})");
            return list;
        }

        foreach (CommandSpecBase spec in step.commands)
        {
            if (spec == null)
            {
                Log("Null command spec in step; skipped.");
                continue;
            }
            
            if (!_commandFactory.TryCreate(spec, out ISequenceCommand cmd) || cmd == null)
            {
                Log($"Failed to create command (specType={spec.GetType().Name})");
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
    
    private static CleanupPolicy DecideCleanupPolicy(CommandRunScope scope)
    {
        if (scope == null)
            return CleanupPolicy.Cancel;

        // Skip은 "즉시 완료 상태"
        if (scope.IsSkipping)
            return CleanupPolicy.Finish;

        return CleanupPolicy.Cancel;
    }
}
