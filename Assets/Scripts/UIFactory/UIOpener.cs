using UnityEngine;

public class UIOpener
{
    private readonly UIRouter _router;

    public UIOpener(UIRouter router) => _router = router;

    public void Open(string route, object payload = null)
        => _router.Navigate(new UIRequest(route, payload));
}
