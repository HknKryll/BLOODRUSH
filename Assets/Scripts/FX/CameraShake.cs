using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public static void Shake(float intensity = 0.15f, float duration = 0.15f)
    {
        if (Instance) Instance.StartCoroutine(Instance.DoShake(intensity, duration));
    }

    IEnumerator DoShake(float intensity, float duration)
    {
        Vector3 origin  = transform.localPosition;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float fade = 1f - t;
            float x = (Mathf.PerlinNoise(elapsed * 80f, 0f) - 0.5f) * 2f * intensity * fade;
            float y = (Mathf.PerlinNoise(0f, elapsed * 80f) - 0.5f) * 2f * intensity * fade;
            transform.localPosition = origin + new Vector3(x, y, 0f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localPosition = origin;
    }
}
