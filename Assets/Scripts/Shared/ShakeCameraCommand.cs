using System.Collections;

public sealed class ShakeCameraCommand : CommandBase
{
    public float strength = 1f;
    public float duration = 0.3f;

    public override bool WaitForCompletion => false;
    public override SkipPolicy SkipPolicy => SkipPolicy.Ignore;

    protected override IEnumerator ExecuteInner(NodePlayScope scope)
    {
        scope.Presenter.ShakeCamera(strength, duration);
        yield break;
    }
}