using UnityEngine;
using UnityEngine.UI;

public class DialogueBootstrap : MonoBehaviour
{
    [Header("Route Catalog")]
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;

    [Header("Presenter")]
    [SerializeField] private MonoBehaviour presenterBehaviour; // IDialoguePresenter 구현 필요

    [Header("Infra")]
    [SerializeField] private UnitySignalBus signalBus;
    [SerializeField] private Button nextButton;

    [Header("Start")]
    [SerializeField] private string startSituationKey = "Intro";

    private DialogueSession _session;
    private DialogueGateRunner _gateRunner;
    private UnityInputSource _input;
    private UnityTimeSource _time;

    private IDialoguePresenter _presenter;

    private void Awake()
    {
        if (routeCatalog == null)
        {
            Debug.LogError("[DialogueBootstrap] routeCatalog is null");
            enabled = false;
            return;
        }

        _presenter = presenterBehaviour as IDialoguePresenter;
        if (_presenter == null)
        {
            Debug.LogError("[DialogueBootstrap] presenterBehaviour must implement IDialoguePresenter");
            enabled = false;
            return;
        }

        _input = new UnityInputSource();
        _time  = new UnityTimeSource();

        var resolver = new DialogueResolver(routeCatalog);
        _gateRunner  = new DialogueGateRunner(_input, _time, signalBus);
        _session     = new DialogueSession(resolver, _gateRunner, _presenter);

        // QA / 로그용 훅은 원하면 다시 추가

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(() =>
            {
                _input.PulseAdvance();
            });
        }

        // CommandPipelinePresenter라면 공유 Context 연결
        if (_presenter is CommandPipelinePresenter cpp)
        {
            cpp.SyncFrom(_session.Context);
        }
    }

    private void Start()
    {
        StartBySituationKey(startSituationKey);
    }

    /// <summary>
    /// ✅ 이제 외부에서 쓸 유일한 진입점: Start(situationKey)
    /// </summary>
    public void StartBySituationKey(string situationKey)
    {
        if (!_session.Start(situationKey))
        {
            Debug.LogError($"[DialogueBootstrap] Failed to start situation: {situationKey}");
        }
    }

    private void Update()
    {
        // Auto / Skip 토글 (테스트용)
        if (Input.GetKeyDown(KeyCode.A))
            _session.Context.IsAutoMode = !_session.Context.IsAutoMode;
        if (Input.GetKeyDown(KeyCode.K))
            _session.Context.IsSkipping = !_session.Context.IsSkipping;

        if (_presenter is CommandPipelinePresenter cpp)
        {
            cpp.SyncFrom(_session.Context);
        }

        _session.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}
