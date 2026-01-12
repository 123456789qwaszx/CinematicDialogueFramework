using UnityEngine;

public sealed class StubEntryPoint : MonoBehaviour
{
    [SerializeField] private RouteCatalogSO routeCatalog;
    [SerializeField] private StubRoutePlayer prp;

    [SerializeField] public string RouteKey = "stub.seq";
    private PresentationSession _session;

    public void StartSession()
    {
        _session = prp.CreateSession();
    }
    
    public void PlayRoute(string routeKey)
    {
        if (routeCatalog == null)
        {
            Debug.LogError("[PresentationRoutePlayer] RouteCatalog is not assigned.");
            return;
        }

        if (!routeCatalog.TryResolve(routeKey, out Route route, out SequenceSpecSO sequence))
        {
            Debug.LogWarning($"[PresentationRoutePlayer] Failed to resolve routeKey='{routeKey}'");
            return;
        }
        
        if (_session == null)
        {
            Debug.LogWarning("[PresentationEntryPoint] Session is null. Call StartSession() before PlayRoute.");
            return;
        }

        _session.Start(route, sequence);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartSession();
            Debug.Log("[PresentationEntryPoint] Session started.");
        }
        
        if (_session != null && _session.IsRunning)
            _session.Tick();
        
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PlayRoute(RouteKey);
            Debug.Log("[PresentationEntryPoint] Started.");
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            RequestEnd();
            Debug.Log("[PresentationEntryPoint] RequestEnd");
        }
    }
    
    public void RequestEnd()
    {
        _session?.RequestEnd();
    }
}