using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.FX
{
public class SpeedEffect : MonoBehaviour
{
    [SerializeField] Camera     playerCamera;
    [SerializeField] float      baseFov        = 70f;
    [SerializeField] float      maxFovBoost    = 25f;
    [SerializeField] float      speedThreshold = 8f;
    [SerializeField] float      maxSpeed       = 35f;
    [SerializeField] float      lerpSpeed      = 6f;

    // Ayarlardaki FOV kaydırıcısı buraya yazar (0 = ayar yok, serialize edilen baseFov
    // kullanılır). Hız artışı bunun ÜSTÜNE bindiği için ayar taban değeri değiştirir.
    public static float FovOverride = 0f;

    CharacterController  cc;
    ChromaticAberration  ca;

    void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) cc = player.GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = Camera.main;

        var vol = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 2;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        ca         = profile.Add<ChromaticAberration>();
        ca.active  = true;
        ca.intensity.Override(0f);
    }

    void Update()
    {
        if (cc == null || playerCamera == null) return;

        float speed = cc.velocity.magnitude;
        float t     = Mathf.Clamp01((speed - speedThreshold) / (maxSpeed - speedThreshold));

        float fovBase = FovOverride > 0f ? FovOverride : baseFov;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            fovBase + maxFovBoost * t,
            Time.deltaTime * lerpSpeed);

        ca.intensity.value = Mathf.Lerp(ca.intensity.value, t, Time.deltaTime * lerpSpeed);
    }
}
}
