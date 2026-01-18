using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandRoutingAttribute : Attribute
{
    public readonly CpsTrackType Track;
    public readonly CpsPhase Phase;

    public CommandRoutingAttribute(CpsTrackType track, CpsPhase phase)
    {
        Track = track;
        Phase = phase;
    }
}