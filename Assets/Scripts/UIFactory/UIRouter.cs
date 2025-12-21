using System;
using System.Collections.Generic;

public class UIRouter
{
    private readonly UIResolver _resolver;
    private readonly UIScreenFactory _factory;
    
    public Dictionary<string, ScreenKey> _routes = new()
    {
        { "home", ScreenKey.Home },
        { "shop", ScreenKey.Shop },
    };

    private UIScreen _current;
    
    public UIRouter(UIResolver resolver, UIScreenFactory factory)
    {
        _resolver = resolver;
        _factory  = factory;
    }

    public void Navigate(UIRequest request)
    {
        ScreenKey key = _routes.GetValueOrDefault(request.route);
        
        UIResolveResult result = _resolver.Resolve(key, request);
        
        UnityEngine.Debug.Log(result.Trace.Dump());
        _current = _factory.Create(result, this);
    }
}

