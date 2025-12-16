using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SituationEntry
{
    // 이 시퀀스 내부에서의 로컬 ID
    public string situationId;

    // ✅ 이제 한 줄 데이터 + 게이트까지 포함하는 노드 리스트
    public List<DialogueNodeSpec> nodes = new();
}

[CreateAssetMenu(fileName = "DialogueSequenceData", menuName = "Dialogue/Sequence")]
public class DialogueSequenceData : ScriptableObject
{
    public string sequenceId;
    public List<SituationEntry> situations = new();
}