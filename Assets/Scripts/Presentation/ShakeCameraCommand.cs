using System.Collections;

public class ShakeCameraCommand : CommandBase
{
    public float strength = 1f;
    public float duration = 0.3f;

    public override bool WaitForCompletion => false; // 그냥 발사만

    public override string DebugName => $"ShakeCamera({strength}, {duration})";

    protected override IEnumerator ExecuteInner(CommandContext ctx)
    {
        ctx.ShakeCamera(strength, duration);
        yield break; // 바로 다음 커맨드로 넘어감
    }

    protected override void OnSkip(CommandContext ctx)
    {
        // 스킵 모드라도 카메라 흔들림은 짧게라도 실행하고 싶다면:
        ctx.ShakeCamera(strength * 0.5f, duration * 0.3f);
    }
}