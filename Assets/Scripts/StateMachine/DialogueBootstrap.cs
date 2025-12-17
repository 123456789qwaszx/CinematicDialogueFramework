using UnityEngine;

public sealed class DialogueBootstrap : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;

    [Header("Ports / Adapters")]
    [SerializeField] private MonoBehaviour presenterBehaviour; // IDialoguePresenter
    [SerializeField] private MonoBehaviour commandExecuter; // INodeExecutor
    [SerializeField] private UnitySignalBus signals;
    
    [Header("Session / Runner")]
    private DialogueSession _session;
    private DialogueGateRunner _gateRunner;
    
    [Header("Starter")]
    private DialogueStarter _dialogueStarter;
    public DialogueStarter DialogueStarter => _dialogueStarter;
    

    private void Awake()
    {
        DialogueResolver resolver       = new (routeCatalog);
        DialogueGatePlanner gatePlanner = new ();
        NodeViewModelBuilder vmBuilder  = new ();

        // Compose runner (subscribes to signals)
        UnityInputSource input        = new();
        UnityTimeSource time          = new();
        DialogueGateRunner gateRunner = new DialogueGateRunner(input, time, signals);

        // Compose output port(s)
        DialogueNodeOutputComposite output = new ((IDialoguePresenter)presenterBehaviour, (INodeExecutor)commandExecuter);

        DialogueSession session  = new (resolver, gatePlanner, gateRunner, vmBuilder, output, routeCatalog);

        _session = session;
        _gateRunner = gateRunner;

        _dialogueStarter = new DialogueStarter(_session);
    }
    
    
    [SerializeField] private bool enableDebugHotkeys = true;
    
    private void Update()
    {
        if (_session == null) return;

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
        
        _session?.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}