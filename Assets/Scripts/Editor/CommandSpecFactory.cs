#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class CommandSpecFactory
{
    public static CommandSpecBase CreateAndBake(Type specType)
    {
        if (specType == null)
            return null;

        if (!typeof(CommandSpecBase).IsAssignableFrom(specType))
        {
            Debug.LogError($"[CommandSpecFactory] Type is not a CommandSpecBase: {specType}");
            return null;
        }

        if (specType.IsAbstract)
        {
            Debug.LogError($"[CommandSpecFactory] Type is abstract: {specType}");
            return null;
        }

        // POCO 인스턴스 생성 (리플렉션은 에디터에서만)
        CommandSpecBase spec;
        try
        {
            spec = (CommandSpecBase)Activator.CreateInstance(specType);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CommandSpecFactory] Failed to create instance: {specType}\n{e}");
            return null;
        }

        // Meta bake
        spec.Editor_SetMeta(CommandMetaDefaults.GetDefault(specType));

        // POCO는 SetDirty가 직접 안 먹는다.
        // 변경을 저장하려면, spec을 담고 있는 상위 에셋(SequenceSpecSO 등)을 SetDirty해야 함.
        return spec;
    }
}
#endif