using System.Collections;

public sealed class ShakeCameraCommand : CommandBase
{
    public float strength = 1f;
    public float duration = 0.2f;

    public override bool WaitForCompletion => false;

    // ✅ Ticket 07: 스킵 시 연출 생략(기본 안전)
    public override SkipPolicy SkipPolicy => SkipPolicy.Ignore;

    protected override IEnumerator ExecuteInner(CommandContext ctx)
    {
        ctx.ShakeCamera(strength, duration);
        yield break;
    }
}
