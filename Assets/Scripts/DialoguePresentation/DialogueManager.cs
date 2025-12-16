using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// 새 파이프라인용 DialogueManager
/// - Resources에서 DialogueSequenceData / DialogueSpeakerData 로딩
/// - 스피커/초상화 캐싱
/// - (sequenceId, situationId)를 받아 Command 리스트를 만들고 실행
/// - 스킵/오토/타임스케일은 CommandContext를 통해 제어
/// </summary>
public class DialogueManager : Singleton<DialogueManager>
{
    [Header("Debug Info")] [SerializeField]
    private bool showDebugInfo = true;

    [Header("Resources Path")] [SerializeField]
    private string sequenceResourcesPath = "Data/Dialogue/Sequence";

    [SerializeField] private string speakerResourcesPath = "Dialogue/Speaker";

    [Header("Command Service")] [SerializeField]
    private CommandServiceConfig commandServiceConfig;

    [Header("Timing Plan")] [SerializeField]
    private TimingPlanSO defaultTimingPlan;

    // ===== Data lookup =====
    private readonly Dictionary<string, DialogueSequenceData> _sequencesById = new();
    private readonly Dictionary<string, DialogueSpeakerData> _speakersById = new();
    private readonly Dictionary<string, Dictionary<Expression, Sprite>> _portraitCache = new();

    // ===== Runtime state =====
    private Coroutine _playRoutine;
    private string _currentSequenceId;
    private string _currentSituationId;
    private bool _isPlaying;

    // Command 실행용
    private CommandService _commandService;
    private CommandContext _commandContext;
    private CancellationTokenSource _cts;

    // ===== WaitInput 게이트용 상태 =====
    private bool _inputGatePending;
    private bool _inputGateTriggered;

    // ===== Events =====
    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;

    /// <summary>대화 종료 시 (sequenceId, situationId) 함께 알려주고 싶을 때</summary>
    public event Action<string, string> OnDialogueEndedWithIds;

    // 외부에서 참고용
    public bool IsPlaying => _isPlaying;
    public string CurrentSequenceId => _currentSequenceId;
    public string CurrentSituationId => _currentSituationId;

    #region Unity lifecycle

    protected override void Awake()
    {
        InitData();
        InitCommandInfra();
    }

    private void OnDestroy()
    {
        CancelAndDisposeCts();
    }

    #endregion

    #region Init

    public void InitData()
    {
        _sequencesById.Clear();
        _speakersById.Clear();
        _portraitCache.Clear();

        LoadAllSequences();
        LoadAllSpeakers();
    }

    private void InitCommandInfra()
    {
        if (commandServiceConfig == null)
        {
            Debug.LogWarning(
                "[DialogueManager] CommandServiceConfig is null. Commands that use services may not work.");
        }

        _commandService = new CommandService(commandServiceConfig);
        _commandContext = new CommandContext(_commandService);
        ResetCancellationToken();
    }

    private void LoadAllSequences()
    {
        DialogueSequenceData[] allSequences = Resources.LoadAll<DialogueSequenceData>(sequenceResourcesPath);

        foreach (DialogueSequenceData seq in allSequences)
        {
            if (string.IsNullOrEmpty(seq.sequenceId))
            {
                Debug.LogWarning($"[DialogueManager] Sequence has empty sequenceId: {seq.name}");
                continue;
            }

            if (!_sequencesById.TryAdd(seq.sequenceId, seq))
            {
                Debug.LogWarning($"[DialogueManager] Duplicate Sequence ID detected: {seq.sequenceId}");
            }
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueManager] Loaded {_sequencesById.Count} DialogueSequenceData assets");
    }

    private void LoadAllSpeakers()
    {
        DialogueSpeakerData[] allSpeakers = Resources.LoadAll<DialogueSpeakerData>(speakerResourcesPath);

        foreach (DialogueSpeakerData speaker in allSpeakers)
        {
            if (string.IsNullOrEmpty(speaker.speakerId))
            {
                Debug.LogWarning($"[DialogueManager] Speaker has empty speakerId: {speaker.name}");
                continue;
            }

            if (!_speakersById.TryAdd(speaker.speakerId, speaker))
            {
                Debug.LogWarning($"[DialogueManager] Duplicate Speaker ID detected: {speaker.speakerId}");
            }

            _portraitCache[speaker.speakerId] = BuildPortraitCache(speaker);
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueManager] Loaded DialogueSpeakerData: {_speakersById.Count} speakers");
    }

    private Dictionary<Expression, Sprite> BuildPortraitCache(DialogueSpeakerData speaker)
    {
        Dictionary<Expression, Sprite> dict = new();

        foreach (PortraitConfig config in speaker.portraitConfigs)
        {
            if (config == null || config.portrait == null)
            {
                Debug.Log($"[DialogueManager] Missing portrait sprite: {speaker.speakerId} / {config?.expression}");
                continue;
            }

            dict[config.expression] = config.portrait;
        }

        return dict;
    }

    #endregion

    #region Public API - 시작/종료

    /// <summary>
    /// (sequenceId, situationId)를 기준으로 대화 시작
    /// </summary>
    public void StartDialogue(string sequenceId, string situationId)
    {
        if (string.IsNullOrEmpty(sequenceId))
        {
            Debug.LogError("[DialogueManager] StartDialogue called with empty sequenceId");
            return;
        }

        if (!_sequencesById.TryGetValue(sequenceId, out DialogueSequenceData sequenceData))
        {
            Debug.LogError($"[DialogueManager] SequenceData not found for ID: {sequenceId}");
            return;
        }

        SituationEntry situation = sequenceData.situations
            .FirstOrDefault(x => x.situationId == situationId);

        if (situation == null)
        {
            Debug.LogError($"[DialogueManager] Situation '{situationId}' not found in sequence '{sequenceId}'");
            return;
        }
        

        // 이미 다른 대화가 재생 중이라면 그것부터 종료 
        QuitExistingDialogue();

        _currentSequenceId = sequenceId;
        _currentSituationId = situationId;
        _isPlaying = true;

        if (showDebugInfo)
        {
            Debug.Log(
                $"[DialogueManager] Dialogue started: {sequenceId} / {situationId}, line count: {situation.lines.Count}");
        }

        OnDialogueStarted?.Invoke();

        // SequenceData + Situation -> Command 리스트 빌드
        List<ISequenceCommand> commands = BuildCommandsFrom(situation);
        
        if (showDebugInfo)
        {
            if (defaultTimingPlan != null && defaultTimingPlan.expectedCommandCount > 0)
            {
                if (defaultTimingPlan.expectedCommandCount != commands.Count)
                {
                    Debug.LogWarning(
                        $"[DialogueManager] TimingPlan expected {defaultTimingPlan.expectedCommandCount} cmds, " +
                        $"but got {commands.Count}. (sequence={sequenceId}, situation={situationId})");
                }
            }
        }

        // 코루틴으로 실행
        _playRoutine = StartCoroutine(PlayCommandsCoroutine(commands, defaultTimingPlan));
    }

    private void QuitExistingDialogue()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        ResetCancellationToken();
    }

    public void EndDialogue()
    {
        if (!_isPlaying) return;
        
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        
        CancelAndDisposeCts();
        
        if (showDebugInfo)
            Debug.Log("[DialogueManager] Dialogue ended");

        _isPlaying = false;

        OnDialogueEnded?.Invoke();
        OnDialogueEndedWithIds?.Invoke(_currentSequenceId, _currentSituationId);

        _currentSequenceId = null;
        _currentSituationId = null;
    }

    #endregion

    #region Command 실행 코루틴

    private IEnumerator PlayCommandsCoroutine(List<ISequenceCommand> commands, TimingPlanSO timingPlan)
    {
        ResetCancellationToken();
        CommandContext ctx = _commandContext;

        if (commands == null || commands.Count == 0)
        {
            EndDialogue();
            yield break;
        }

        int cmdIndex = 0;
        int tokenIndex = 0;

        while (cmdIndex < commands.Count)
        {
            if (ctx.Token.IsCancellationRequested)
                yield break;

            TimingTokenSpec token = null;
            if (timingPlan != null && tokenIndex < timingPlan.tokens.Count)
                token = timingPlan.tokens[tokenIndex];

            // 이번 토큰이 소비할 커맨드 개수
            int consumeCount = token != null
                ? Mathf.Max(1, token.consumeCount)
                : commands.Count - cmdIndex; // 토큰이 더 없으면 남은 커맨드 전부 실행

            // 1) 이 토큰이 담당하는 커맨드 묶음 실행
            for (int i = 0; i < consumeCount && cmdIndex < commands.Count; i++)
            {
                ISequenceCommand cmd = commands[cmdIndex++];

                if (ctx.Token.IsCancellationRequested)
                    yield break;

                IEnumerator routine = cmd.Execute(ctx);

                if (cmd.WaitForCompletion && routine != null)
                {
                    // ShowLineCommand의 경우 → ShowLineCoroutine(타이핑 끝까지) 대기
                    yield return routine;
                }
                else if (routine != null)
                {
                    // 동시에 돌려도 되는 연출은 그냥 발사
                    StartCoroutine(routine);
                }
            }

            // 2) 이 묶음 이후의 Gate 대기
            if (token != null)
            {
                yield return WaitTimingGate(token.gate, ctx);
                tokenIndex++;
            }
        }

        EndDialogue();
    }

    /// <summary>
    /// SituationEntry -> ISequenceCommand 리스트로 변환
    /// (필요하면 여기서 라인마다 효과 커맨드도 삽입 가능)
    /// </summary>
    private List<ISequenceCommand> BuildCommandsFrom(SituationEntry situation)
    {
        var commands = new List<ISequenceCommand>();

        foreach (DialogueLine line in situation.lines)
        {
            if (line == null)
                continue;

            // 1) 기본: 한 줄 보여주는 커맨드
            commands.Add(new ShowLineCommand(line));

            // 2) 예시: 화난 표정이면 카메라 약간 흔들기
            // if (line.expression == Expression.Angry)
            // {
            //     commands.Add(new ShakeCameraCommand { strength = 0.8f, duration = 0.2f });
            // }

            // 3) 필요하면 추가 Command 붙이기 (SE, BGM, 컷인 등)
        }

        return commands;
    }

    #endregion

    #region Skip / Auto / TimeScale 제어

    public void SetSkip(bool isSkipping)
    {
        if (_commandContext == null) return;
        _commandContext.IsSkipping = isSkipping;
    }

    public void SetAutoMode(bool isAuto)
    {
        if (_commandContext == null) return;
        _commandContext.IsAutoMode = isAuto;
    }

    public void SetTimeScale(float timeScale)
    {
        if (_commandContext == null) return;
        _commandContext.TimeScale = Mathf.Max(0f, timeScale);
    }

    #endregion

    #region Speaker / Portrait Helper (옵션)

    public bool TryGetSpeaker(string speakerId, out DialogueSpeakerData speaker)
        => _speakersById.TryGetValue(speakerId, out speaker);

    /// <summary>
    /// 스피커 + 표정으로 초상화 스프라이트 조회 (없으면 Default 사용, 그마저도 없으면 null)
    /// </summary>
    public Sprite GetSpeakerPortrait(string speakerId, Expression expression)
    {
        if (!_portraitCache.TryGetValue(speakerId, out Dictionary<Expression, Sprite> dict))
        {
            Debug.LogWarning($"[DialogueManager] No portrait dictionary found for speakerId: {speakerId}");
            return null;
        }

        if (dict.TryGetValue(expression, out Sprite sprite))
            return sprite;

        if (dict.TryGetValue(Expression.Default, out Sprite defaultSprite))
            return defaultSprite;

        return null;
    }

    /// <summary>
    /// TimingGateSpec에 따라 커맨드 묶음 사이의 대기를 처리
    /// - DelaySeconds  : 시간 대기
    /// - WaitInput     : 플레이어 입력 대기 (NotifyInputGate로 신호)
    /// - WaitSignal    : TODO (외부 연출 신호 시스템에 연결 예정)
    /// - WaitFlagInt   : TODO (게임 상태 플래그 시스템에 연결 예정)
    /// </summary>
    private IEnumerator WaitTimingGate(TimingGateSpec gate, CommandContext ctx)
    {
        switch (gate.type)
        {
            case TimingGateType.Immediate:
                // 바로 다음 커맨드 묶음으로 진행
                yield break;

            case TimingGateType.DelaySeconds:
            {
                float duration = Mathf.Max(0f, gate.delaySeconds);
                float t = 0f;

                while (t < duration)
                {
                    if (ctx.Token.IsCancellationRequested)
                        yield break;

                    if (ctx.IsSkipping) // 스킵이면 더 기다릴 필요 없음
                        yield break;

                    t += Time.deltaTime * ctx.TimeScale;
                    yield return null;
                }

                break;
            }

            case TimingGateType.WaitInput:
            {
                // UIDialogue 등에서 NotifyInputGate()가 불릴 때까지 대기
                _inputGatePending = true;
                _inputGateTriggered = false;

                if (showDebugInfo)
                    Debug.Log("[DialogueManager] Waiting for input gate...");

                while (!_inputGateTriggered)
                {
                    if (ctx.Token.IsCancellationRequested || ctx.IsSkipping)
                    {
                        _inputGatePending = false;
                        _inputGateTriggered = false;
                        yield break;
                    }

                    yield return null;
                }

                _inputGatePending = false;
                _inputGateTriggered = false;
                break;
            }

            case TimingGateType.WaitSignal:
            {
                // TODO: 나중에 Timeline/연출 시스템과 연동해서 신호를 받을 수 있게 확장
                // ex) while (!SignalBus.Has(gate.signalId)) yield return null;
                yield break;
            }

            case TimingGateType.WaitFlagInt:
            {
                // TODO: 게임 상태 플래그 시스템에 맞춰 구현
                // ex) while (GameFlags.GetInt(gate.flagKey) != gate.compareValue) yield return null;
                yield break;
            }

            default:
                yield break;
        }
    }

    #endregion

    /// <summary>
    /// WaitInput 게이트를 통과시키는 신호
    /// - UIDialogue.OnClickNext 등에서 호출
    /// </summary>
    public void NotifyInputGate()
    {
        if (_inputGatePending)
        {
            _inputGateTriggered = true;
        }
    }

    #region CancellationToken 관리

    private void ResetCancellationToken()
    {
        CancelAndDisposeCts();
        _cts = new CancellationTokenSource();
        if (_commandContext != null)
        {
            _commandContext.Token = _cts.Token;
        }
    }

    private void CancelAndDisposeCts()
    {
        if (_cts != null)
        {
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

    #endregion
}