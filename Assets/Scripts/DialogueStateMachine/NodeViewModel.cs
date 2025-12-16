/// <summary>
/// A display-oriented ViewModel to pass to the Presenter.
/// A data bundle derived from RuntimeState + Spec.
/// 이제 표준 "한 줄" 데이터는 DialogueLine을 그대로 안고 간다.
/// </summary>
public readonly struct NodeViewModel
{
    public readonly string SituationKey;
    public readonly int NodeIndex;

    /// <summary>
    /// 상태머신/파이프라인 공통의 한 줄 데이터
    /// </summary>
    public readonly DialogueLine Line;

    public readonly string BranchKey;
    public readonly string VariantKey;

    public readonly int TokenCount;

    // ---- 편의 프로퍼티 (기존 코드 호환용) ----
    public string SpeakerId => Line != null ? Line.speakerId : string.Empty;
    public string Text      => Line != null ? Line.text      : string.Empty;
    public Expression Expression => Line != null ? Line.expression : Expression.Default;
    public DialoguePosition Position => Line != null ? Line.position : DialoguePosition.Left;

    public NodeViewModel(
        string situationKey,
        int nodeIndex,
        DialogueLine line,
        string branchKey,
        string variantKey,
        int tokenCount)
    {
        SituationKey = situationKey;
        NodeIndex    = nodeIndex;
        Line         = line;
        BranchKey    = branchKey;
        VariantKey   = variantKey;
        TokenCount   = tokenCount;
    }
}