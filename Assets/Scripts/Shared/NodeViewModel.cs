using System.Collections.Generic;

/// <summary>
/// Presenter에 넘겨주는 "노드 단위" ViewModel.
/// - 한 노드 안의 커맨드 묶음(CommandSpecs)
/// - 그 중 대표 라인(PrimaryLine)을 함께 담고 있음
/// </summary>
public readonly struct NodeViewModel
{
    public readonly string SituationKey;
    public readonly int NodeIndex;

    /// <summary>
    /// 이 노드를 구성하는 커맨드 스펙 묶음
    /// </summary>
    public readonly IReadOnlyList<NodeCommandSpec> CommandSpecs;

    /// <summary>
    /// 텍스트 위주 Presenter(예: TMP UI)를 위한 대표 대사 라인
    /// - 보통 첫 번째 ShowLine 커맨드
    /// </summary>
    public readonly DialogueLine PrimaryLine;

    public readonly string BranchKey;
    public readonly string VariantKey;

    public readonly int TokenCount;

    // ---- 기존 코드 호환용 편의 프로퍼티 ----

    public string SpeakerId => PrimaryLine != null ? PrimaryLine.speakerId : string.Empty;
    public string Text      => PrimaryLine != null ? PrimaryLine.text      : string.Empty;
    public Expression Expression => PrimaryLine != null ? PrimaryLine.expression : Expression.Default;
    public DialoguePosition Position => PrimaryLine != null ? PrimaryLine.position : DialoguePosition.Left;

    public NodeViewModel(
        string situationKey,
        int nodeIndex,
        IReadOnlyList<NodeCommandSpec> commandSpecs,
        DialogueLine primaryLine,
        string branchKey,
        string variantKey,
        int tokenCount)
    {
        SituationKey = situationKey;
        NodeIndex    = nodeIndex;
        CommandSpecs = commandSpecs;
        PrimaryLine  = primaryLine;
        BranchKey    = branchKey;
        VariantKey   = variantKey;
        TokenCount   = tokenCount;
    }
}