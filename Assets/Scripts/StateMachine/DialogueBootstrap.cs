using UnityEngine;

public sealed class DialogueBootstrap : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;
    [SerializeField] private CommandServiceConfig commandServiceConfig;

    [Header("Ports / Adapters")]
    [SerializeField] private MonoBehaviour presenterBehaviour; // IDialoguePresenter
    [SerializeField] private MonoBehaviour commandExecuter; // INodeExecutor
    [SerializeField] private UnitySignalBus signals;
    
    [Header("Session / Runner")]
    private DialogueSession _session;
    private StepGateAdvancer _gateRunner;
    
    [Header("Starter")]
    private DialogueStarter _dialogueStarter;
    public DialogueStarter DialogueStarter => _dialogueStarter;
    

    private void Awake()
    {
        DialogueResolver resolver       = new (routeCatalog);
        StepGatePlanBuilder gatePlanner = new ();
        NodeViewModelBuilder vmBuilder  = new ();

        // Compose runner (subscribes to signals)
        UnityInputSource input        = new();
        UnityTimeSource time          = new();
        StepGateAdvancer gateRunner = new StepGateAdvancer(input, time, signals);

        // Optional extension ports
        CommandService service = new CommandService(commandServiceConfig);
        CommandExecutor executor = commandExecuter as CommandExecutor;
        SequencePlayer sequencePlayer = new(executor);
        DefaultNodeCommandFactory nodeFactory = new DefaultNodeCommandFactory();
        executor.Initialize(sequencePlayer, nodeFactory);
        DialoguePlaybackModes playbackModes = new ();
        
        // Compose output ports
        DialogueNodeOutputComposite output = new ((IDialoguePresenter)presenterBehaviour, (INodeExecutor)commandExecuter);

        
        DialogueSession session  = new (resolver, gatePlanner, gateRunner, vmBuilder, output, routeCatalog, service, playbackModes);

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
                _session.Context.Modes.IsAutoMode = !_session.Context.IsAutoMode;
                Debug.Log($"[Dialogue] AutoMode = {_session.Context.IsAutoMode}");
            }
        
            if (Input.GetKeyDown(KeyCode.K))
            {
                _session.Context.Modes.IsSkipping = !_session.Context.IsSkipping;
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