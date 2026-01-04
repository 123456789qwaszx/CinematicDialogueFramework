using System;

[Serializable]
public abstract class CommandSpecBase
{
    public string screenId;
    public string widgetId;
}

[Serializable]
public sealed class DefaultShowLineCommandSpec : CommandSpecBase
{
    public DialogueLine line;
}

[Serializable]
public sealed class DefaultShakeCameraCommandSpec : CommandSpecBase
{
    public float strength = 1f;
    public float duration = 0.2f;
}