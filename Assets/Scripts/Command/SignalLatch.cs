using System;
using System.Collections.Generic;

public sealed class SignalLatch : ISignalLatch
{
    private readonly HashSet<string> _latched = new(StringComparer.Ordinal);

    public void Latch(string key)
    {
        if (!string.IsNullOrEmpty(key))
            _latched.Add(key);
    }

    public bool IsLatched(string key)
        => !string.IsNullOrEmpty(key) && _latched.Contains(key);

    public bool Consume(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return _latched.Remove(key);
    }

    public void Clear() => _latched.Clear();
}