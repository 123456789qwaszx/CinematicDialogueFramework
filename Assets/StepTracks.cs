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

    public List<CommandSpecBase> Get(CpsTrackType t) => t switch
    {
        CpsTrackType.Interaction => interaction,
        CpsTrackType.Setup       => setup,
        CpsTrackType.Motion      => motion,
        CpsTrackType.Dialogue    => dialogue,
        CpsTrackType.FX          => fx,
        _ => setup
    };
}