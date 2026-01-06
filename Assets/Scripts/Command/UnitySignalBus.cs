using System;
using UnityEngine;

public sealed class UnitySignalBus : MonoBehaviour, ISignalBus
{
    public event Action<string> OnSignal;

    public void Raise(string key) => OnSignal?.Invoke(key);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
            Raise("s");
    }
}