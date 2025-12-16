using UnityEngine;

public sealed class UnityTimeSource : ITimeSource
{
    public float UnscaledDeltaTime => Time.unscaledDeltaTime;
}