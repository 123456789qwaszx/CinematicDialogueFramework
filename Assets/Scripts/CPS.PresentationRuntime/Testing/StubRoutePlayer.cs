using UnityEngine;

public sealed class StubRoutePlayer : MonoBehaviour
{
    [SerializeField] private UnitySignalBus signals;
    [SerializeField] private CommandExecutor commandExecutor;

    private StepGatePlanBuilder _gatePlanner;
    private StepGateAdvancer _gateAdvancer;
    private INodeCommandFactory _commandFactory;
    
    private SequencePlayer _sequencePlayer;

    public bool IsBootstrapped { get; private set; }
    
    public void Bootstrap()
    {
        if (IsBootstrapped)
            return;
        
        if (commandExecutor == null)
        {
            Debug.LogError("[PresentationRoutePlayer] CommandExecutor is not assigned.");
            return;
        }
        
        if (signals == null)
        {
            GameObject go = new GameObject("[Auto] UnitySignalBus");
            signals = go.AddComponent<UnitySignalBus>();
            Debug.Log("[PresentationRoutePlayer] signals not found, created new UnitySignalBus GameObject.");
        }
        
        _gatePlanner = new StepGatePlanBuilder();
        
        UnityInputSource input      = new();
        UnityTimeSource time        = new();
        SignalLatch latch           = new();
        
        signals.OnSignal += latch.Latch;
        
        _gateAdvancer = new StepGateAdvancer(input, time, signals, latch);
        _commandFactory = new StubCommandFactory(time, signals, latch);
        
        _sequencePlayer = new SequencePlayer(commandExecutor);
        commandExecutor.Initialize(_sequencePlayer, _commandFactory);

        IsBootstrapped = true;
    }

    public PresentationSession CreateSession(PlaybackSettings playbackSettings = null)
    {
        if (!IsBootstrapped)
            Bootstrap();
        
        playbackSettings ??= new PlaybackSettings();
        
        return new PresentationSession(_gatePlanner, _gateAdvancer, commandExecutor, playbackSettings);
    }
    
     private void OnDestroy()
     {
         _gateAdvancer?.Dispose();
     }
}