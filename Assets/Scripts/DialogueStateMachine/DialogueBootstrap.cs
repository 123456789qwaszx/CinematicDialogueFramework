using UnityEngine;
using UnityEngine.UI;

public class DialogueBootstrap : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private DialogueCatalog catalog;
    [SerializeField] private UIDialoguePresenterTMP presenter;
    [SerializeField] private UnitySignalBus signalBus;
    [SerializeField] private Button nextButton;

    [Header("Start")] [SerializeField] private string startSituationKey = "Intro";

    private DialogueSession _session;
    private DialogueGateRunner _gateRunner;
    private UnityInputSource _input;
    private UnityTimeSource _time;

    private void Awake()
    {
        _input = new UnityInputSource();
        _time = new UnityTimeSource();

        var resolver = new DialogueResolver(catalog);
        _gateRunner = new DialogueGateRunner(_input, _time, signalBus);
        _session = new DialogueSession(resolver, _gateRunner, presenter);

        // QA / 로그용 훅
        _session.OnNodeEntered += vm =>
            Debug.Log($"[Node] {vm.SituationKey} #{vm.NodeIndex} ({vm.BranchKey}/{vm.VariantKey}) tokens={vm.TokenCount}");
        _session.OnTokenProgress += (c, n) =>
            Debug.Log($"[Gate] token {c}/{n}");
        _session.OnSituationEnded += () =>
            Debug.Log("[Dialogue] ended");

        if (nextButton != null)
            nextButton.onClick.AddListener(() => _input.PulseAdvance());
    }

    private void Start()
    {
        if (!_session.Start(startSituationKey))
            Debug.LogError($"Failed to start: {startSituationKey}");
    }

    private void Update()
    {
        // Auto / Skip 토글 (테스트용)
        if (Input.GetKeyDown(KeyCode.A))
            _session.Context.IsAutoMode = !_session.Context.IsAutoMode;
        if (Input.GetKeyDown(KeyCode.K))
            _session.Context.IsSkipping = !_session.Context.IsSkipping;

        _session.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}