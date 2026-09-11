using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// ADIM 1+2 — Kırmızı acil durum lambası. Sürekli, düzensiz aralıklarla yanıp söner.
//
// HDRP'de ışık şiddeti Light.intensity'den DEĞİL, HDAdditionalLightData üzerinden lümen
// olarak ayarlanıyor (proje geneli kural). Onu yakalayıp lümeni oynatıyoruz; bulunamazsa
// Light.enabled ile yetiniyoruz — yani ışık her hâlükârda yanıp söner.
[RequireComponent(typeof(Light))]
public class EmergencyLight : MonoBehaviour
{
    [Header("Yanıp sönme")]
    [Tooltip("Yanık kaldığı süre aralığı (min, max sn).")]
    [SerializeField] Vector2 onRange  = new Vector2(0.7f, 1.4f);
    [Tooltip("Sönük kaldığı süre aralığı (min, max sn).")]
    [SerializeField] Vector2 offRange = new Vector2(0.25f, 0.6f);
    [Tooltip("Yanıkken lümen değeri.")]
    [SerializeField] float   litLumen = 900f;
    [Tooltip("Sönükken tamamen kapansın mı, yoksa kısık mı kalsın (lümen).")]
    [SerializeField] float   dimLumen = 0f;
    [Tooltip("Yanma/sönme geçiş süresi. 0 = sert açma-kapama.")]
    [SerializeField] float   rampTime = 0.08f;

    Light                  lamp;
    HDAdditionalLightData  hd;

    void Awake()
    {
        lamp = GetComponent<Light>();
        hd   = GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = gameObject.AddComponent<HDAdditionalLightData>();
        hd.EnableShadows(false);   // kırpışan gölge çok pahalı ve gürültülü olur
    }

    void OnEnable()  => StartCoroutine(Blink());
    void OnDisable() => StopAllCoroutines();

    IEnumerator Blink()
    {
        while (true)
        {
            yield return Ramp(litLumen);
            yield return new WaitForSeconds(Random.Range(onRange.x, onRange.y));
            yield return Ramp(dimLumen);
            yield return new WaitForSeconds(Random.Range(offRange.x, offRange.y));
        }
    }

    IEnumerator Ramp(float target)
    {
        if (rampTime <= 0f) { SetLumen(target); yield break; }

        float from = CurrentLumen();
        float t = 0f;
        while (t < rampTime)
        {
            t += Time.deltaTime;
            SetLumen(Mathf.Lerp(from, target, t / rampTime));
            yield return null;
        }
        SetLumen(target);
    }

    // Olceksiz uzayda calis: Ramp() hedefi de olceksiz, ikisi ayni birimde olmali.
    float CurrentLumen()
    {
        float raw = hd != null ? hd.intensity : lamp.intensity;
        return raw / Mathf.Max(0.01f, DarkSceneExposure.LightScale);
    }

    void SetLumen(float lumen)
    {
        float scaled = lumen * DarkSceneExposure.LightScale;
        if (hd != null) hd.SetIntensity(scaled, LightUnit.Lumen);
        else            lamp.intensity = scaled;

        // Tamamen sönükse ışığı kapat — HDRP'de 0 lümen yine de maliyet üretir.
        lamp.enabled = lumen > 0.01f;
    }
}
}
