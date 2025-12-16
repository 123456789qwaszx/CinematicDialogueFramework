using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Dialogue/Demo/Situation Spec", fileName = "DialogueSituationSpec")]
public class DialogueSituationSpec : ScriptableObject
{
    public string situationKey = "Intro";

    public List<DialogueNodeSpec> nodes = new();
}

[Serializable]
public class DialogueNodeSpec
{
    public string speakerId = "Narrator";

    [TextArea(3, 10)] public string text;

    // Ensure at least one token per node (if none, insert an Immediately token).
    public List<GateToken> gateTokens = new() { GateToken.Input() };
}