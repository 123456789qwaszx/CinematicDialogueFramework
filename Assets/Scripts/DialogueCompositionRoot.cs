using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ✅ 이 클래스가 "유일한 실행 엔트리"이자 Composition Root.
///
/// 여기서만:
/// - Repository 초기화(선택)
/// - RouteCatalog 주입
/// - Resolver / GateRunner / Session 조립
/// - Presenter 연결 (TMP / CommandPipeline 등)
/// - 입력/시간/신호 소스 연결
/// - 매 프레임 Session.Tick() 호출
///
/// 나머지 클래스들은 전부 "순수 서비스/데이터"만 담당.
/// </summary>
public sealed class DialogueCompositionRoot : MonoBehaviour
{
    [Header("Route Catalog (SituationKey → Sequence/Situation)")]
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;

    [Header("Presenter (IDialoguePresenter 구현체)")]
    [SerializeField] private MonoBehaviour presenterBehaviour; // UIDialoguePresenterTMP 혹은 CommandPipelinePresenter 등

    [Header("Infra")]
    [SerializeField] private UnitySignalBus signalBus;
    [SerializeField] private Button nextButton;

    [Header("Debug / Testing")]
    [SerializeField] private bool autoStartOnAwake = true;
    [SerializeField] private string startSituationKey = "Intro";

    [SerializeField] private bool enableDebugHotkeys = true;
    
    // A: 상태머신 코어
    private DialogueSession _session;
    private DialogueGateRunner _gateRunner;
    private UnityInputSource _input;
    private UnityTimeSource _time;

    // B: Presenter 실제 타입
    private IDialoguePresenter _presenter;

    // C: 선택적으로 Repository (데이터 창고)도 여기서 보장 초기화 가능
    [Header("Optional: DialogueRepository 초기화")]
    [SerializeField] private bool initRepositoryOnAwake = true;

    private void Awake()
    {
        // 0) (선택) Repository 초기화: 전역 데이터 창고 (Sequence / Speaker / Portrait)
        if (initRepositoryOnAwake && DialogueRepository.Instance != null)
        {
            DialogueRepository.Instance.InitData();
        }

        // 1) 필수 의존성 체크
        if (routeCatalog == null)
        {
            Debug.LogError("[DialogueCompositionRoot] routeCatalog is null");
            enabled = false;
            return;
        }

        _presenter = presenterBehaviour as IDialoguePresenter;
        if (_presenter == null)
        {
            Debug.LogError("[DialogueCompositionRoot] presenterBehaviour must implement IDialoguePresenter");
            enabled = false;
            return;
        }

        if (signalBus == null)
        {
            // 없어도 돌아갈 수는 있지만 Signal GateToken은 못씀
            Debug.LogWarning("[DialogueCompositionRoot] signalBus is null. Signal 게이트는 동작하지 않습니다.");
            // 필요하면 여기서 AddComponent<UnitySignalBus>()로 동적으로 붙여도 됨
        }

        // 2) 상태머신 코어 조립 (Resolver + GateRunner + Session)
        _input = new UnityInputSource();
        _time  = new UnityTimeSource();

        var resolver = new DialogueResolver(routeCatalog);
        _gateRunner  = new DialogueGateRunner(_input, _time, signalBus);
        _session     = new DialogueSession(resolver, _gateRunner, _presenter);

        // 3) CommandPipelinePresenter라면 Context 공유 연결
        if (_presenter is CommandPipelinePresenter cpp)
        {
            cpp.SyncFrom(_session.Context);
        }

        // 4) Next 버튼 → InputSource에 펄스
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(() =>
            {
                _input.PulseAdvance();
            });
        }

        // 5) 필요하면 QA용 로그 훅 (원하면 활성화해서 사용)
        // _session.OnNodeEntered += vm =>
        //     Debug.Log($"[Node] {vm.SituationKey} #{vm.NodeIndex} tokens={vm.TokenCount}");
        // _session.OnTokenProgress += (cursor, count) =>
        //     Debug.Log($"[Gate] token {cursor}/{count}");
        // _session.OnSituationEnded += () =>
        //     Debug.Log("[Dialogue] Situation ended");
    }

    private void Start()
    {
        if (autoStartOnAwake && !string.IsNullOrEmpty(startSituationKey))
        {
            StartBySituationKey(startSituationKey);
        }
    }

    /// <summary>
    /// ✅ 외부에서 호출할 수 있는 유일한 진입점:
    /// 상황 키 하나만 알고 있으면 된다.
    /// </summary>
    public void StartBySituationKey(string situationKey)
    {
        if (_session == null)
        {
            Debug.LogError("[DialogueCompositionRoot] Session is null. Awake에서 조립이 실패한 것 같습니다.");
            return;
        }

        if (!_session.Start(situationKey))
        {
            Debug.LogError($"[DialogueCompositionRoot] Failed to start situation: {situationKey}");
        }
        else
        {
            // CommandPipelinePresenter 사용 중이면 Context 최신 상태로 다시 Sync
            if (_presenter is CommandPipelinePresenter cpp)
            {
                cpp.SyncFrom(_session.Context);
            }
        }
    }

    private void Update()
    {
        if (_session == null) return;

        // 디버그용 Hotkey (Auto/Skip 토글 등)
        if (enableDebugHotkeys)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                _session.Context.IsAutoMode = !_session.Context.IsAutoMode;
                Debug.Log($"[Dialogue] AutoMode = {_session.Context.IsAutoMode}");
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                _session.Context.IsSkipping = !_session.Context.IsSkipping;
                Debug.Log($"[Dialogue] IsSkipping = {_session.Context.IsSkipping}");
            }
        }

        // CommandPipelinePresenter라면 매 프레임 Context Sync
        if (_presenter is CommandPipelinePresenter cpp)
        {
            cpp.SyncFrom(_session.Context);
        }

        // ✅ 실행 루프는 오직 여기에서만: 상태머신 한 번 Tick
        _session.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}
