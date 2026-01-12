using System.Collections;
using UnityEngine;

public class StubCommandFactory : INodeCommandFactory
{
    private readonly ITimeSource _time;
    private readonly ISignalBus _signal;
    private readonly ISignalLatch _latch;
    
    public StubCommandFactory(ITimeSource time, ISignalBus signal, ISignalLatch latch)
    {
        _time   = time;
        _signal = signal;
        _latch = latch;
    }
    
    public bool TryCreate(CommandSpecBase spec, out ISequenceCommand command)
    {
        command = null;
        if (spec == null) return false;

        switch (spec)
        {
            case StubCommandSpec s:
            {
                command = new StubCommand()
                { };
                return command != null;
            }
            
            default:
                return false;
        }
    }
}


public sealed class StubCommandSpec : CommandSpecBase
{
    
}


public sealed class StubCommand : CommandBase
{
    public StubCommand() { }
    
    public override bool WaitForCompletion => false;
    protected override SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
    
    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        Debug.Log("StubCommand");
        yield break;
    }
}