#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class CommandMetaDefaults
{
    private static readonly Dictionary<Type, CommandMeta> Cache = new();

    public static CommandMeta GetDefault(Type t)
    {
        if (t == null) return default;
        if (Cache.TryGetValue(t, out var meta)) return meta;

        meta = default;

        // 1) routing
        var routing = (CommandRoutingAttribute)Attribute.GetCustomAttribute(t, typeof(CommandRoutingAttribute));
        if (routing != null)
        {
            meta.track = (CommandTrackType)(int)routing.Track;
            meta.phase = (CommandPhase)(int)routing.Phase;
        }

        // 2) timing hint
        var timing = (CommandTimingHintAttribute)Attribute.GetCustomAttribute(t, typeof(CommandTimingHintAttribute));
        if (timing != null)
        {
            meta.blockingHint = timing.Blocking;
            meta.infiniteHint = timing.Infinite;
            meta.durationHint = timing.Duration;
        }

        Cache[t] = meta;
        return meta;
    }

    public static void ClearCache() => Cache.Clear();
}
#endif