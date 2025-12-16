using UnityEngine;

public class UnityInputSource : IInputSource
{
    private bool _pulse;

    public void PulseAdvance() => _pulse = true;

    public bool ConsumeAdvancePressed()
    {
        bool pressed = _pulse || Input.GetKeyDown(KeyCode.Space);
        _pulse = false;
        return pressed;
    }
}