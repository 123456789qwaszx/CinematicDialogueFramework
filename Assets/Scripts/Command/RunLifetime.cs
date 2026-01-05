using System;
using System.Collections.Generic;

public enum CleanupPolicy { Cancel, Finish }

public class RunLifetime
{
    private readonly List<(Action cancel, Action finish)> _items = new();

    public void Track(Action cancel, Action finish = null)
    {
        if (cancel == null && finish == null) return;
        _items.Add((cancel, finish));
    }

    public void Cleanup(CleanupPolicy policy)
    {
        for (int i = _items.Count - 1; i >= 0; --i)
        {
            var (cancel, finish) = _items[i];
            try
            {
                if (policy == CleanupPolicy.Finish) (finish ?? cancel)?.Invoke();
                else cancel?.Invoke();
            }
            catch { /* cleanup은 방어적으로 */ }
        }

        _items.Clear();
    }

    public void Clear() => _items.Clear();
}