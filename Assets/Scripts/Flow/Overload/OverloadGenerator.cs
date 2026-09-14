using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// Jeneratörün görsel/işitsel hâli. Hiçbir kural bilmiyor — sadece 0-1 arası bir şarj
// değeri alıp onu gösteriyor. ChargeZone.onChargeChanged buraya bağlanıyor.
//
// Gösterge üç katmanlı, çünkü karanlık/kalabalık bir çatışma sırasında tek bir ipucu
// gözden kaçıyor: (1) gövde parlaklığı, (2) segment çubuğu — kaç kutu yandı, (3) sesin
// tizleşmesi. Üçü de aynı değeri anlatıyor.
public class OverloadGenerator : MonoBehaviour
{
    [Header("Gövde parlaması")]
    [SerializeField] Renderer glowRenderer;
    [SerializeField] Color    emptyColor = new Color(0.55f, 0.12f, 0.08f);
    [SerializeField] Color    fullColor  = new Color(0.35f, 1f, 0.55f);
    [Tooltip("Emissive çarpanı. Boşken de tamamen sönük olmasın diye taban var.")]
    [SerializeField] float    minGlow = 1.2f;
    [SerializeField] float    maxGlow = 5f;

    [Header("Segment çubuğu")]
    [Tooltip("Soldan sağa dolan kutular. OverloadRoomBuilder otomatik atar.")]
    [SerializeField] Renderer[] segments;
    [SerializeField] Color      segmentOff = new Color(0.08f, 0.08f, 0.09f);

    [Header("Işık")]
    [SerializeField] Light lightSource;
    [SerializeField] float minLumen = 200f;
    [SerializeField] float maxLumen = 2600f;

    [Header("Ses")]
    [Tooltip("Sürekli dönen uğultu (loop). Şarj arttıkça tizleşir ve yükselir.")]
    [SerializeField] AudioSource hum;
    [SerializeField] float minPitch = 0.65f;
    [SerializeField] float maxPitch = 1.45f;
    [SerializeField] float minVolume = 0.15f;
    [SerializeField] float maxVolume = 0.6f;

    [Header("Kıvılcım")]
    [Tooltip("Bu şarj oranının üstünde kıvılcım başlar.")]
    [Range(0f, 1f)]
    [SerializeField] float sparkThreshold = 0.6f;
    [SerializeField] ParticleSystem sparks;

    Material glowMat;
    Material[] segMats;
    HDAdditionalLightData hdLight;
    float shown;

    void Awake()
    {
        if (glowRenderer != null) glowMat = glowRenderer.material;   // kendi kopyası

        if (segments != null)
        {
            segMats = new Material[segments.Length];
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null) segMats[i] = segments[i].material;
        }

        if (lightSource != null) hdLight = lightSource.GetComponent<HDAdditionalLightData>();

        if (hum != null)
        {
            hum.loop = true;
            if (hum.clip != null && !hum.isPlaying) hum.Play();
        }

        if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        SetCharge(0f);
    }

    // ChargeZone.onChargeChanged (UnityEvent<float>) buraya bağlanır.
    public void SetCharge(float t)
    {
        shown = Mathf.Clamp01(t);

        Color c = Color.Lerp(emptyColor, fullColor, shown);
        if (glowMat != null)
        {
            Color e = c * Mathf.Lerp(minGlow, maxGlow, shown);
            if (glowMat.HasProperty("_UnlitColor"))    glowMat.SetColor("_UnlitColor", e);
            if (glowMat.HasProperty("_EmissiveColor")) glowMat.SetColor("_EmissiveColor", e);
            if (glowMat.HasProperty("_BaseColor"))     glowMat.SetColor("_BaseColor", e);
        }

        if (segMats != null)
        {
            // Kaç segment yanmalı: 0.31 şarj + 10 segment → 3 tam kutu.
            int lit = Mathf.RoundToInt(shown * segMats.Length);
            for (int i = 0; i < segMats.Length; i++)
            {
                if (segMats[i] == null) continue;
                Color sc = i < lit ? c * maxGlow : segmentOff;
                if (segMats[i].HasProperty("_UnlitColor"))    segMats[i].SetColor("_UnlitColor", sc);
                if (segMats[i].HasProperty("_EmissiveColor")) segMats[i].SetColor("_EmissiveColor", sc);
                if (segMats[i].HasProperty("_BaseColor"))     segMats[i].SetColor("_BaseColor", sc);
            }
        }

        if (lightSource != null)
        {
            lightSource.color = c;
            float lumen = Mathf.Lerp(minLumen, maxLumen, shown);
            if (hdLight != null) hdLight.SetIntensity(lumen, LightUnit.Lumen);
            else                 lightSource.intensity = Mathf.Lerp(1f, 6f, shown);
        }

        if (hum != null)
        {
            hum.pitch  = Mathf.Lerp(minPitch, maxPitch, shown);
            hum.volume = Mathf.Lerp(minVolume, maxVolume, shown);
        }

        if (sparks != null)
        {
            bool on = shown >= sparkThreshold;
            if (on && !sparks.isEmitting)      sparks.Play();
            else if (!on && sparks.isEmitting) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // OverloadRoomBuilder kurulum sırasında çağırır.
    public void Configure(Renderer glow, Renderer[] bar, Light light, AudioSource humSource, ParticleSystem sparkFx)
    {
        glowRenderer = glow;
        segments     = bar;
        lightSource  = light;
        hum          = humSource;
        sparks       = sparkFx;
    }
}
}
