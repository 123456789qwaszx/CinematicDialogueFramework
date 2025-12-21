using UnityEngine;

public class UITestDriver : MonoBehaviour
{
    [SerializeField] private UIBootStrap bootstrap;
    private UIOpener _uiOpener;

    void Start()
    {
        if (bootstrap == null)
            bootstrap = FindFirstObjectByType<UIBootStrap>();
        
        _uiOpener = bootstrap.Opener;
    }
    
    public void OnOpenHome()
    {
        _uiOpener.Open("home");
    }

    public void OnOpenShop()
    {
        _uiOpener.Open("shop");
    }
}
