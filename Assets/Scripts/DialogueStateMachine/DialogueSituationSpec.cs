using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Demo/Situation Spec", fileName = "DialogueSituationSpec")]
public class DialogueSituationSpec : ScriptableObject
{
    public string situationKey = "Intro";

    public List<DialogueNodeSpec> nodes = new();
}
