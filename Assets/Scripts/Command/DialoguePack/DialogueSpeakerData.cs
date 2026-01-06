// using System;
// using System.Collections.Generic;
// using UnityEngine;
//
//
// #region dialogueLine
// public enum DialoguePosition { Left, Center, Right }
//
// [Serializable]
// public class DialogueLine
// {
//     public string speakerId = "";
//     public Expression expression = Expression.Default;
//     [TextArea(3, 10)] public string text = "";
//     public DialoguePosition position = DialoguePosition.Left;
// }
// #endregion
//
//
// public enum Expression
// {
//     Default,
//     Happy,
//     Sad,
//     Angry,
//     Surprised,
// }
//
// [System.Serializable]
// public class PortraitConfig
// {
//     public Expression expression;
//     public Sprite portrait;
// }
//
// [CreateAssetMenu(fileName="DialogueSpeakerData", menuName="Dialogue/Speaker")]
// public class DialogueSpeakerData : ScriptableObject
// {
//     public string speakerId;
//     public string displayName;
//     public List<PortraitConfig> portraitConfigs = new();
// }