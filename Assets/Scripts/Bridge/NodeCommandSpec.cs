using System;
using UnityEngine;

/// <summary>
/// 한 노드 안에서 실행할 "연출 커맨드"의 스펙.
/// - ShowLine: 대사 1줄 출력
/// - ShakeCamera: 카메라 흔들기
///   (필요해지면 여기서 타입을 계속 늘리면 됨)
/// </summary>
public enum NodeCommandKind
{
    ShowLine,
    ShakeCamera,
    // TODO: PlaySE, PlayBGM, CutIn, ChangeBackground 등 계속 확장
}

[Serializable]
public sealed class NodeCommandSpec
{
    [Header("Command Type")]
    public NodeCommandKind kind = NodeCommandKind.ShowLine;

    [Header("ShowLine")]
    public DialogueLine line;

    [Header("ShakeCamera")]
    public float shakeStrength = 1f;
    public float shakeDuration = 0.2f;
}