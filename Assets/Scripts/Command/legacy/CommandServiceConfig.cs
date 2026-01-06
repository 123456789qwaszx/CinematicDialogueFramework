// using UnityEngine;
//
// [CreateAssetMenu(
//     fileName = "CommandServiceConfig",
//     menuName = "Dialogue/Command Service Config")]
// public class CommandServiceConfig : ScriptableObject
// {
//     [Header("SO 어댑터 Drag & Drop")]
//     [SerializeField] private CameraShakeAsset cameraShakeAsset;
//     [SerializeField] private DialogueViewAsset dialogueViewAsset;
//
//     // CommandService가 인터페이스로 쓰도록 꺼내주는 프로퍼티
//     public ICameraShakeService CameraShakeService => cameraShakeAsset;
//     public IDialogueViewService DialogueViewService => dialogueViewAsset;
// }