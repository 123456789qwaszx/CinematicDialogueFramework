using System;
using UnityEngine;

public sealed class UnitySignalBus : MonoBehaviour, ISignalBus
{
    public event Action<string> OnSignal;

    public void Raise(string key) => OnSignal?.Invoke(key);

    // 데모용: S 키로 "BattleEnd" 시그널 발사
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
            Raise("BattleEnd");
    }
}