using UnityEngine;
using UnityEngine.UI;

/// <summary>
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
    private IDialoguePresenter _presenterBehaviour;

    [Header("Infra")]
    [SerializeField] private UnitySignalBus signalBus;

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
        _presenter = _presenterBehaviour;

        _input = new UnityInputSource();
        _time  = new UnityTimeSource();

        DialogueResolver resolver = new (routeCatalog);
        _gateRunner  = new DialogueGateRunner(_input, _time, signalBus);
        //_session     = new DialogueSession(resolver, _gateRunner, _presenter);

        // if (_presenter is CommandPipelinePresenter cpp)
        // {
        //     cpp.SyncFrom(_session.Context);
        // }
    }

    private void Start()
    {
        if (autoStartOnAwake && !string.IsNullOrEmpty(startSituationKey))
        {
            //StartBySituationKey(startSituationKey);
            StartBySituationKey("Intro");
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
            // if (_presenter is CommandPipelinePresenter cpp)
            // {
            //     cpp.SyncFrom(_session.Context);
            // }
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
        // if (_presenter is CommandPipelinePresenter cpp)
        // {
        //     cpp.SyncFrom(_session.Context);
        // }

        // ✅ 실행 루프는 오직 여기에서만: 상태머신 한 번 Tick
        _session.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}
