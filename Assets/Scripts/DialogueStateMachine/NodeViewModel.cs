/// <summary>
/// A display-oriented ViewModel to pass to the Presenter.
/// A data bundle derived from RuntimeState + Spec.
/// </summary>
public readonly struct NodeViewModel
{
    public readonly string SituationKey;
    public readonly int NodeIndex;

    public readonly string SpeakerId;
    public readonly string Text;

    public readonly string BranchKey;
    public readonly string VariantKey;

    public readonly int TokenCount;

    public NodeViewModel(
        string situationKey,
        int nodeIndex,
        string speakerId,
        string text,
        string branchKey,
        string variantKey,
        int tokenCount)
    {
        SituationKey = situationKey;
        NodeIndex = nodeIndex;
        SpeakerId = speakerId;
        Text = text;
        BranchKey = branchKey;
        VariantKey = variantKey;
        TokenCount = tokenCount;
    }
}