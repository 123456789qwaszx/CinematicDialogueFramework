using System.Collections.Generic;

public static class StepCompiler
{
    private static readonly CpsPhase[] PhaseOrder =
    {
        CpsPhase.Setup, CpsPhase.Motion, CpsPhase.Dialogue, CpsPhase.FX, CpsPhase.Teardown
    };

    private static readonly CpsTrackType[] TrackOrder =
    {
        CpsTrackType.Interaction, CpsTrackType.Setup, CpsTrackType.Motion, CpsTrackType.Dialogue, CpsTrackType.FX
    };

    public static void CompileInto(StepSpec step)
    {
        step.compiled.Clear();

        foreach (CpsPhase phase in PhaseOrder)
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

    private static IEnumerable<List<CommandSpecBase>> EnumerateTrackLists(StepTracks t, CpsTrackType[] order)
    {
        foreach (var tr in order)
        {
            yield return tr switch
            {
                CpsTrackType.Interaction => t.interaction,
                CpsTrackType.Setup       => t.setup,
                CpsTrackType.Motion      => t.motion,
                CpsTrackType.Dialogue    => t.dialogue,
                CpsTrackType.FX          => t.fx,
                _ => t.setup
            };
        }
    }
}