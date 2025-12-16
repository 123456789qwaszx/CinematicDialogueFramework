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
    [Header("Display / Content")]
    public DialogueLine line = new DialogueLine
    {
        speakerId  = "Narrator",
        expression = Expression.Default,
        text       = "",
        position   = DialoguePosition.Left
    };

    [Header("Progression Gate")]
    public List<GateToken> gateTokens = new() { GateToken.Input() };
}