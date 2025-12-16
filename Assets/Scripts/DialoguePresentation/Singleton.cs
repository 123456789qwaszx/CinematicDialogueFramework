using UnityEngine;

/// <summary>
/// MonoBehaviour 기반 싱글톤 추상 클래스
/// 파생 클래스에서 Instance 호출로 접근 가능
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // 실제 인스턴스를 저장할 정적 필드
    private static T instance;
    
    // DDO 여부
    protected virtual bool ShouldDontDestroyOnLoad => false;

    // 외부에서 접근 가능한 정적 프로퍼티
    public static T Instance
    {
        get
        {
            // 인스턴스가 없으면 씬에서 찾아봄
            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();

                // 못 찾았으면 경고 로그 출력
                if (instance == null)
                {
                    Debug.Log($"[Singleton<{typeof(T)}>] 인스턴스가 씬에 존재하지 않습니다.");
                }
            }

            return instance;
        }
    }

    // Awake에서 자기 자신이 싱글톤 인스턴스인지 검사 (중복 방지)
    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            
            if (ShouldDontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning($"[Singleton<{typeof(T)}>] 중복 인스턴스가 존재하므로 파괴됩니다.");
            Destroy(gameObject);
        }
    }
}