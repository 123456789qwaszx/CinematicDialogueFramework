using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandTimingHintAttribute : Attribute
{
    public readonly bool Blocking;
    public readonly bool Infinite;
    public readonly float Duration;

    public CommandTimingHintAttribute(bool blocking = false, bool infinite = false, float duration = 0f)
    {
        Blocking = blocking;
        Infinite = infinite;
        Duration = duration;
    }
}