using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandRoutingAttribute : Attribute
{
    public readonly CommandTrackType Track;
    public readonly CommandPhase Phase;

    public CommandRoutingAttribute(CommandTrackType track, CommandPhase phase)
    {
        Track = track;
        Phase = phase;
    }
}