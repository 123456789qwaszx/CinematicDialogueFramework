using System.Collections;

public sealed class ShakeCameraCommand : CommandBase
{
    private readonly ICameraShakeService _cameraShake;
    private readonly float _strength;
    private readonly float _duration;
    
    public ShakeCameraCommand(ICameraShakeService cameraShake, float strength, float duration)
    {
        _cameraShake = cameraShake;
        _strength = strength;
        _duration = duration;
    }

    public override bool WaitForCompletion => false;
    public override SkipPolicy SkipPolicy => SkipPolicy.Ignore;

    protected override IEnumerator ExecuteInner()
    {
        _cameraShake.Shake(_strength, _duration);
        yield break;
    }
}