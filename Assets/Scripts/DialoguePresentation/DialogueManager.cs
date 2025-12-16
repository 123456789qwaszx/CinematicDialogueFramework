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
    
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;

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

// ===== GateRunner (통합 게이트) =====
    [Header("Gate Runner (Unified)")]
    [SerializeField] private UnitySignalBus signalBus; // WaitSignal용
    [SerializeField] private float autoAdvanceDelay = 0.6f; // Auto 모드일 때 Input 토큰 자동 진행 딜레이

    private UnityInputSource _gateInput;
    private UnityTimeSource _gateTime;
    private DialogueGateRunner _gateRunner;

// Pipeline에서도 GateRunner.Tick을 쓰기 위한 "더미 런타임 상태"
    private readonly DialogueRuntimeState _gateState = new DialogueRuntimeState();
    private DialogueContext _gateContext;
    
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
        InitUnifiedGateInfra();
    }
    
    private void InitUnifiedGateInfra()
    {
        _gateInput = new UnityInputSource();
        _gateTime  = new UnityTimeSource();

        // signalBus가 null이어도 컴파일은 되지만, WaitSignal은 동작하지 않음
        if (signalBus == null)
            Debug.LogWarning("[DialogueManager] signalBus is null. WaitSignal gates will never pass.");

        _gateRunner = new DialogueGateRunner(_gateInput, _gateTime, signalBus);

        _gateContext = new DialogueContext
        {
            IsAutoMode = false,
            IsSkipping = false,
            TimeScale = 1f,
            AutoAdvanceDelay = autoAdvanceDelay
        };
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
    public void StartDialogue(string situationKey)
    {
        if (routeCatalog == null)
        {
            Debug.LogError("[DialogueManager] routeCatalog is null (required for StartDialogue(situationKey))");
            return;
        }

        if (!routeCatalog.TryGetRoute(situationKey, out DialogueRoute route))
        {
            Debug.LogError($"[DialogueManager] Route not found: {situationKey}");
            return;
        }

        if (route.Kind != DialogueRouteKind.Pipeline)
        {
            Debug.LogError($"[DialogueManager] Route '{situationKey}' is not Pipeline kind");
            return;
        }

        if (route.Sequence == null || string.IsNullOrEmpty(route.PipelineSituationId))
        {
            Debug.LogError($"[DialogueManager] Pipeline route invalid: {situationKey}");
            return;
        }

        // timingPlanOverride가 있으면 그걸 쓰고, 없으면 기존 defaultTimingPlan 사용
        var plan = route.TimingPlanOverride != null ? route.TimingPlanOverride : defaultTimingPlan;

        // ✅ 기존 로직 재사용 (약간만 확장: timingPlan 파라미터 받게)
        StartDialogue(route.SequenceId, route.PipelineSituationId, plan);
    }
    
    public void StartDialogue(string sequenceId, string situationId)
    {
        StartDialogue(sequenceId, situationId, defaultTimingPlan);
    }

    /// <summary>
    /// (sequenceId, situationId)를 기준으로 대화 시작
    /// </summary>
    private void StartDialogue(string sequenceId, string situationId, TimingPlanSO timingPlan)
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
        //_playRoutine = StartCoroutine(PlayCommandsCoroutine(commands, defaultTimingPlan));
        _playRoutine = StartCoroutine(PlayCommandsCoroutine(commands, timingPlan));
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

    private List<ISequenceCommand> BuildCommandsFrom(SituationEntry situation)
    {
        var commands = new List<ISequenceCommand>();

        if (situation.nodes == null)
            return commands;

        foreach (DialogueNodeSpec node in situation.nodes)
        {
            if (node == null || node.line == null)
                continue;

            DialogueLine line = node.line;

            // 1) 기본: 한 줄 보여주는 커맨드
            commands.Add(new ShowLineCommand(line));

            // 2) 예시: 화난 표정이면 카메라 약간 흔들기
            // if (line.expression == Expression.Angry)
            // {
            //     commands.Add(new ShakeCameraCommand { strength = 0.8f, duration = 0.2f });
            // }

            // 3) 필요하면 node.gateTokens를 참조해서
            //    "이 라인 전에/후에 특정 연출 커맨드를 추가" 같은 것도 가능
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

    private IEnumerator WaitTimingGate(TimingGateSpec gate, CommandContext ctx)
    {
        if (_gateRunner == null)
            InitUnifiedGateInfra();

        // TimingGateSpec -> GateToken 리스트로 변환
        List<GateToken> tokens = TimingGateTokenMapper.ToTokens(gate);

        // 더미 상태에 Gate 설정
        _gateState.Gate.Tokens = tokens;
        _gateState.Gate.TokenCursor = 0;
        _gateState.Gate.InFlight = default;

        // GateContext를 CommandContext에서 동기화
        SyncGateContextFromCommand(ctx);

        // Session처럼 "같은 프레임에 즉시 토큰은 끝까지 소비" 가능하게 처리
        while (!_gateState.IsNodeGateCompleted)
        {
            if (ctx.Token.IsCancellationRequested)
                yield break;

            SyncGateContextFromCommand(ctx);

            bool progressed = false;

            // 한 프레임 내에서 가능한 만큼 진행
            while (!_gateState.IsNodeGateCompleted)
            {
                bool moved = _gateRunner.Tick(_gateState, _gateContext);
                if (!moved) break;
                progressed = true;
            }

            if (_gateState.IsNodeGateCompleted)
                yield break;

            // 진행 못 했으면 다음 프레임 대기
            if (!progressed)
                yield return null;
            else
                yield return null;
        }
    }

    private void SyncGateContextFromCommand(CommandContext ctx)
    {
        _gateContext.IsAutoMode = ctx.IsAutoMode;
        _gateContext.IsSkipping = ctx.IsSkipping;
        _gateContext.TimeScale = ctx.TimeScale <= 0f ? 0.01f : ctx.TimeScale;
        _gateContext.AutoAdvanceDelay = autoAdvanceDelay <= 0f ? 0.4f : autoAdvanceDelay;
    }

    #endregion

    /// <summary>
    /// ✅ 통합된 입력 게이트 통과 신호
    /// - 이제는 플래그를 올리는 게 아니라, GateRunner가 읽을 수 있도록 Pulse를 쏜다.
    /// - (DialogueBootstrap의 Next 버튼이 이 메서드를 호출해도 됨)
    /// </summary>
    public void NotifyInputGate()
    {
        _gateInput?.PulseAdvance();
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