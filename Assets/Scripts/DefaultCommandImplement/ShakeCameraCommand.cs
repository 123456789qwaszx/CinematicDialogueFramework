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
    
    protected override SkipPolicy SkipPolicy => SkipPolicy.Ignore;
    public override bool WaitForCompletion => false;
    
    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        _cameraShake.Shake(_strength, _duration);
        yield break;
    }
}