using System.Collections.Generic;

public static class StepCompiler
{
    private static readonly CommandPhase[] PhaseOrder =
    {
        CommandPhase.Setup, CommandPhase.Motion, CommandPhase.Dialogue, CommandPhase.FX, CommandPhase.Teardown
    };

    private static readonly CommandTrackType[] TrackOrder =
    {
        CommandTrackType.Interaction, CommandTrackType.Setup, CommandTrackType.Motion, CommandTrackType.Dialogue, CommandTrackType.FX
    };

    public static void CompileInto(StepSpec step)
    {
        step.compiled.Clear();

        foreach (CommandPhase phase in PhaseOrder)
        {
            foreach (List<CommandSpecBase> list in EnumerateTrackLists(step.tracks, TrackOrder))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    CommandSpecBase commandSpec = list[i];
                    if (commandSpec == null)
                        continue;

                    if (commandSpec.Meta.phase != phase) continue;
                    step.compiled.Add(commandSpec);
                }
            }
        }
    }

    private static IEnumerable<List<CommandSpecBase>> EnumerateTrackLists(StepTracks t, CommandTrackType[] order)
    {
        foreach (var tr in order)
        {
            yield return tr switch
            {
                CommandTrackType.Interaction => t.interaction,
                CommandTrackType.Setup       => t.setup,
                CommandTrackType.Motion      => t.motion,
                CommandTrackType.Dialogue    => t.dialogue,
                CommandTrackType.FX          => t.fx,
                _ => t.setup
            };
        }
    }
}