#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
//using UnityEditor.TypeCache;

[InitializeOnLoad]
public static class CommandMetaDefaults
{
    private static readonly Dictionary<Type, CommandMeta> _cache = new();

    static CommandMetaDefaults()
    {
        BuildCache();
    }

    public static CommandMeta GetDefault(Type t)
    {
        if (_cache.TryGetValue(t, out var m)) return m;
        return new CommandMeta { track = CommandTrackType.Setup, phase = CommandPhase.Setup };
    }

    private static void BuildCache()
    {
        _cache.Clear();

        foreach (var t in TypeCache.GetTypesDerivedFrom<CommandSpecBase>())
        {
            if (t.IsAbstract) continue;

            var routing = t.GetCustomAttribute<CommandRoutingAttribute>(false);
            var timing  = t.GetCustomAttribute<CommandTimingHintAttribute>(false);

            var meta = new CommandMeta
            {
                track = routing?.Track ?? CommandTrackType.Setup,
                phase = routing?.Phase ?? CommandPhase.Setup,

                blockingHint = timing?.Blocking ?? false,
                infiniteHint = timing?.Infinite ?? false,
                durationHint = timing?.Duration ?? 0f,
            };

            _cache[t] = meta;
        }
    }
}
#endif