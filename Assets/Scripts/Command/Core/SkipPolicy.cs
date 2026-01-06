public enum SkipPolicy
{
    /// <summary>When skipping, do not execute this command (default).</summary>
    Ignore = 0,

    /// <summary>When skipping, treat it as completed immediately (calls OnSkip).</summary>
    CompleteImmediately = 1,

    /// <summary>Execute even while skipping (e.g., critical system presentation / state changes).</summary>
    ExecuteEvenIfSkipping = 2 
}