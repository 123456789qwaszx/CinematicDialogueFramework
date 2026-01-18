using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StepTracks
{
    [SerializeReference] public List<CommandSpecBase> interaction = new();
    [SerializeReference] public List<CommandSpecBase> setup       = new();
    [SerializeReference] public List<CommandSpecBase> motion      = new();
    [SerializeReference] public List<CommandSpecBase> dialogue    = new();
    [SerializeReference] public List<CommandSpecBase> fx          = new();

    public List<CommandSpecBase> Get(CommandTrackType t) => t switch
    {
        CommandTrackType.Interaction => interaction,
        CommandTrackType.Setup       => setup,
        CommandTrackType.Motion      => motion,
        CommandTrackType.Dialogue    => dialogue,
        CommandTrackType.FX          => fx,
        _ => setup
    };
}