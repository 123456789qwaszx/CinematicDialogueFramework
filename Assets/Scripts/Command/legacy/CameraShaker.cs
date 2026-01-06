// using UnityEngine;
// using Unity.Cinemachine;
//
// public class CameraShaker : MonoBehaviour
// {
//     public CinemachineImpulseSource impulseSource;
//
//     private void Awake()
//     {
//         impulseSource = GetComponent<CinemachineImpulseSource>();
//     }
//     
//     /// <summary>
//     /// 카메라 흔들림 적용 
//     /// </summary>
//     public void Shake(float strength = -1f, float duration = -1f)
//     {
//         float finalStrength = (strength > 0f) ? strength : 1f;
//         float finalDuration = (duration > 0f) ? duration : 1f;
//
//         // 방향과 강도 지정
//         impulseSource.DefaultVelocity = Vector3.one * finalStrength;
//
//         // 실제 발사
//         impulseSource.GenerateImpulse();
//     
//         Debug.Log($"[CameraShaker] Impulse generated with strength {finalStrength}, duration {finalDuration}");
//     }
// }