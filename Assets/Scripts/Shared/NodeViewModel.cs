public readonly struct NodeViewModel
{
    public readonly string SituationKey;
    public readonly int NodeIndex;

    public readonly string SpeakerId;
    public readonly string Text;
    public readonly Expression Expression;
    public readonly DialoguePosition Position;

    public readonly string BranchKey;
    public readonly string VariantKey;

    public readonly int TokenCount;

    public NodeViewModel(
        string situationKey,
        int nodeIndex,
        string speakerId,
        string text,
        Expression expression,
        DialoguePosition position,
        string branchKey,
        string variantKey,
        int tokenCount)
    {
        SituationKey = situationKey;
        NodeIndex    = nodeIndex;
        SpeakerId    = speakerId;
        Text         = text;
        Expression   = expression;
        Position     = position;
        BranchKey    = branchKey;
        VariantKey   = variantKey;
        TokenCount   = tokenCount;
    }

    public static NodeViewModel System(
        string situationKey,
        int nodeIndex,
        string message,
        int tokenCount = 0)
    {
        return new NodeViewModel(
            situationKey: situationKey ?? "(none)",
            nodeIndex: nodeIndex,
            speakerId: "System",
            text: message ?? string.Empty,
            expression: Expression.Default,
            position: DialoguePosition.Left,
            branchKey: "Default",
            variantKey: "Default",
            tokenCount: tokenCount
        );
    }
}