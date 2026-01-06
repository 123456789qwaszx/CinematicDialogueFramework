// using UnityEngine;
//
// [CreateAssetMenu(fileName = "DefaultCameraShake", menuName = "Dialogue/Camera Shake/Default")]
// public class DefaultCameraShakeAsset : CameraShakeAsset
// {
//     public float globalStrengthMultiplier = 1f;
//
//     public override void Shake(float strength, float duration)
//     {
//         if (CameraShaker.Instance == null) return;
//         CameraShaker.Instance.Shake(strength * globalStrengthMultiplier, duration);
//     }
// }