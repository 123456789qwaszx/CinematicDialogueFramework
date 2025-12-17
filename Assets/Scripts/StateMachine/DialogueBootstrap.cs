using UnityEngine;

public sealed class DialogueBootstrap : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DialogueRouteCatalogSO routeCatalog;

    [Header("Ports / Adapters")]
    [SerializeField] private MonoBehaviour presenterBehaviour; // IDialoguePresenter
    [SerializeField] private UnityInputSource input;
    [SerializeField] private UnityTimeSource time;
    [SerializeField] private UnitySignalBus signals;
    
    [Header("Session / Runner")]
    private DialogueSession _session;
    private DialogueGateRunner _gateRunner;
    
    //[Header("Starter")]
    //private DialogueStarter _dialogueStarter;
    //public DialogueStarter DialogueStarter => _dialogueStarter;
    

    private void Awake()
    {
        DialogueResolver resolver       = new (routeCatalog);
        DialogueGatePlanner gatePlanner = new ();
        NodeViewModelBuilder vmBuilder  = new ();

        // Compose runner (subscribes to signals)
        DialogueGateRunner gateRunner = new DialogueGateRunner(input, time, signals);

        // Compose output port(s)
        DialogueNodeOutputComposite output = new ((IDialoguePresenter)presenterBehaviour);

        DialogueSession session  = new (resolver, gatePlanner, gateRunner, vmBuilder, output, routeCatalog);

        _session = session;
        _gateRunner = gateRunner;

        //_dialogueStarter = new DialogueStarter(_session);
    }
    
    
    [SerializeField] private bool tickInUpdate = true;
    private void Update()
    {
        if (!tickInUpdate) return;
        _session?.Tick();
    }

    private void OnDestroy()
    {
        _gateRunner?.Dispose();
    }
}