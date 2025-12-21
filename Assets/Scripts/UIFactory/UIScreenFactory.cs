using UnityEngine;

public class UIScreenFactory
{
    private readonly Transform _uiRoot;
    private readonly UIBinder  _binder;
    private readonly UIPatchApplier _patcher;
    private readonly UIComposer _composer;

    public UIScreenFactory(
        Transform uiRoot,
        UIBinder binder,
        UIPatchApplier patcher,
        UIComposer composer)
    {
        _uiRoot   = uiRoot;
        _binder   = binder;
        _patcher  = patcher;
        _composer = composer;
    }

    public UIScreen Create(UIResolveResult result, UIRouter router)
    {
        ResolvedUIScreen resolved = result.Resolved;

        GameObject prefab = resolved.Prefab;
        if (prefab == null)
        {
            Debug.LogError($"Resolved prefab is null for screenId={resolved.ScreenId}");
            return null;
        }

        GameObject go = Object.Instantiate(prefab, _uiRoot);
        UIScreen screen = go.GetComponent<UIScreen>();

        screen.Build(_binder);

        _patcher.Apply(screen, result.Patches);

        _composer.Compose(screen, resolved.BaseSpec, router);

        return screen;
    }
}