using System.Collections;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public static void Do(float duration = 0.06f, float scale = 0.05f)
    {
        if (Instance) Instance.StartCoroutine(Instance.Freeze(duration, scale));
    }

    IEnumerator Freeze(float duration, float scale)
    {
        Time.timeScale       = scale;
        Time.fixedDeltaTime  = 0.02f * scale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale       = 1f;
        Time.fixedDeltaTime  = 0.02f;
    }
}
