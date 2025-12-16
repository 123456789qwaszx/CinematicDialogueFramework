using UnityEngine;
using UnityEngine.UI;

public class DialogueBootstrap : MonoBehaviour
{
    [Header("Route Catalog")]
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;

    [Header("Refs (StateMachine)")]
    [SerializeField] private MonoBehaviour presenterBehaviour; // IDialoguePresenter 구현 필요
    [SerializeField] private UnitySignalBus signalBus;
    [SerializeField] private Button nextButton;

    [Header("Refs (Pipeline)")]
    [SerializeField] private DialogueManager dialogueManager; // 비어있으면 DialogueManager.Instance 사용

    [Header("Start")]
    [SerializeField] private string startSituationKey = "Intro";

    private DialogueSession _session;
    private DialogueGateRunner _gateRunner;
    private UnityInputSource _input;
    private UnityTimeSource _time;

    private IDialoguePresenter _presenter;

    private DialogueRouteKind _activeKind;

    private void Awake()
    {
        if (routeCatalog == null)
        {
            Debug.LogError("[DialogueBootstrap] routeCatalog is null");
            enabled = false;
            return;
        }

        // Presenter 캐스팅 검사
        _presenter = presenterBehaviour as IDialoguePresenter;
        if (_presenter == null)
        {
            Debug.LogError("[DialogueBootstrap] presenterBehaviour must implement IDialoguePresenter");
            enabled = false;
            return;
        }

        // StateMachine 인프라 준비 (Pipeline만 실행해도 준비해두는 게 안전)
        _input = new UnityInputSource();
        _time  = new UnityTimeSource();

        var resolver = new DialogueResolver(routeCatalog);
        _gateRunner  = new DialogueGateRunner(_input, _time, signalBus);
        _session     = new DialogueSession(resolver, _gateRunner, _presenter);

        // next 버튼: 상태머신 Advance + 파이프라인 WaitInput 게이트 둘 다 통과 시도
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(() =>
            {
                _input.PulseAdvance();

                var mgr = dialogueManager != null ? dialogueManager : DialogueManager.Instance;
                if (mgr != null) mgr.NotifyInputGate();
            });
        }

        // (선택) CommandPipelinePresenter라면 Context 동기화
        if (_presenter is CommandPipelinePresenter cpp)
            cpp.SyncFrom(_session.Context);
    }

    private void Start()
    {
        StartBySituationKey(startSituationKey);
    }

    /// <summary>
    /// ✅ 외부에서 호출할 유일한 진입점: Start(situationKey)
    /// </summary>
    public void StartBySituationKey(string situationKey)
    {
        if (!routeCatalog.TryGetRoute(situationKey, out var route))
        {
            Debug.LogError($"[DialogueBootstrap] Route not found: {situationKey}");
            return;
        }

        _activeKind = route.Kind;

        switch (route.Kind)
        {
            case DialogueRouteKind.StateMachine:
            {
                if (!_session.Start(situationKey))
                    Debug.LogError($"[DialogueBootstrap] Failed to start StateMachine: {situationKey}");
                break;
            }

            case DialogueRouteKind.Pipeline:
            {
                var mgr = dialogueManager != null ? dialogueManager : DialogueManager.Instance;
                if (mgr == null)
                {
                    Debug.LogError("[DialogueBootstrap] DialogueManager not found for Pipeline route");
                    return;
                }

                mgr.StartDialogue(situationKey);
                break;
            }
        }
    }

    private void Update()
    {
        // Debug 토글: 상태머신 + 파이프라인 둘 다 반영
        if (Input.GetKeyDown(KeyCode.A))
        {
            _session.Context.IsAutoMode = !_session.Context.IsAutoMode;

            var mgr = dialogueManager != null ? dialogueManager : DialogueManager.Instance;
            if (mgr != null) mgr.SetAutoMode(_session.Context.IsAutoMode);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            _session.Context.IsSkipping = !_session.Context.IsSkipping;

            var mgr = dialogueManager != null ? dialogueManager : DialogueManager.Instance;
            if (mgr != null) mgr.SetSkip(_session.Context.IsSkipping);
        }

        // Presenter가 CommandPipelinePresenter이면 Context 싱크
        if (_presenter is CommandPipelinePresenter cpp)
            cpp.SyncFrom(_session.Context);

        // StateMachine route일 때만 Tick 돌림
        if (_activeKind == DialogueRouteKind.StateMachine)
            _session.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}
