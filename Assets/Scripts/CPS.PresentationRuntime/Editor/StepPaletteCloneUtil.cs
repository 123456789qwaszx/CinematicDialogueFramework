#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class StepPaletteCloneUtil
{
    public static CommandSpecBase CloneCommand(CommandSpecBase source)
    {
        if (source == null) return null;

        string json = JsonUtility.ToJson(source);
        var clone = (CommandSpecBase)Activator.CreateInstance(source.GetType());
        JsonUtility.FromJsonOverwrite(json, clone);
        return clone;
    }

    public static List<CommandSpecBase> CloneCommands(IReadOnlyList<CommandSpecBase> src)
    {
        var list = new List<CommandSpecBase>(src?.Count ?? 0);
        if (src == null) return list;

        for (int i = 0; i < src.Count; i++)
            list.Add(CloneCommand(src[i]));

        return list;
    }
}
#endif