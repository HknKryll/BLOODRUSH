using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.Player;

namespace Bloodrush.FX
{
public class SceneVolumeSetup : MonoBehaviour
{
    [Header("PS1 Stil")]
    [SerializeField] bool enablePS1Style = true;

    [Header("Glitch Tırmanışı")]
    [Tooltip("Sahne bazlı taban bozulma: Ch1-2: 0, Ch3: 0.25, Ch4: 0.5, Ch5: 0.8, Ch6: 1")]
    [SerializeField] [Range(0f, 1f)] float glitchLevel = 0f;

    [Header("Pozlama (Sabit) — opsiyonel")]
    [Tooltip("KAPALI = HDRP oto-pozlaması (ilk sıcak görünüm böyleydi). Sadece bir sahne fazla karanlık/parlaksa AÇ ve değeri ayarla. DİKKAT: değer YÜKSELDİKÇE görüntü KARARIR.")]
    [SerializeField] bool  fixedExposure      = false;
    [SerializeField] [Range(5f, 16f)] float fixedExposureValue = 11f;

    [Header("İç Mekan Dolgu Işığı (Ambient)")]
    [Tooltip("Kapalı mekanda her yüzeye (tavan dahil) düşük taban ışığı verir — simsiyah köşe kalmaz. Server room hissi için AÇ.")]
    [SerializeField] bool  ambientFill      = true;
    [SerializeField] Color ambientColor     = new Color(0.85f, 0.9f, 1f);   // hafif soğuk (floresan)
    [SerializeField] [Range(0f, 3f)] float ambientIntensity = 0.8f;

    [Header("Sis")]
    [Tooltip("KÜÇÜK = daha yoğun sis. 120 ince (temiz bölüm), 40-60 yoğun (derin tesis). SurfacePalette buraya yazar.")]
    [SerializeField] float fogMeanFreePath = 120f;

    Fog              fogComp;
    CustomPassVolume cpv;
    PixelatePass     pixelPass;
    FilmGrain        grain;
    ColorAdjustments colorAdj;
    Exposure         exposureComp;

    void Awake()
    {
        var vol      = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;   // şablonun "Sky and Fog" volume'unu yensin
        var profile  = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile  = profile;

        // Exposure'ı HER ZAMAN oluştur (toggle Play modunda çalışsın diye)
        exposureComp = profile.Add<Exposure>();
        exposureComp.active = true;

        // İç mekan ambient dolgusu — düz gri "gökyüzü" ambient probe'u besler,
        // ışık ulaşmayan yüzeyler (tavan, köşeler) simsiyah kalmaz
        if (ambientFill)
        {
            var env = profile.Add<VisualEnvironment>();
            env.skyType.Override((int)SkyType.Gradient);

            var sky = profile.Add<GradientSky>();
            sky.top.Override(ambientColor);
            sky.middle.Override(ambientColor);
            sky.bottom.Override(ambientColor);
            sky.multiplier.Override(ambientIntensity);
        }

        var bloom = profile.Add<Bloom>();
        bloom.active = true;
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.5f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(new Color(1f, 0.75f, 0.75f));

        colorAdj = profile.Add<ColorAdjustments>();
        colorAdj.active = true;
        colorAdj.contrast.Override(4f);    // gölgeler yumuşak, ezilmeden okunur
        colorAdj.saturation.Override(-12f);

        grain = profile.Add<FilmGrain>();
        grain.active = true;
        grain.intensity.Override(0.12f);
        grain.response.Override(0.85f);

        fogComp = profile.Add<Fog>();
        fogComp.active = true;
        fogComp.enabled.Override(true);
        fogComp.albedo.Override(new Color(0.5f, 0.45f, 0.4f));   // sıcak krem, mavi değil
        fogComp.meanFreePath.Override(fogMeanFreePath);         // palet/Inspector'dan ayarlanır
        fogComp.baseHeight.Override(0f);
        fogComp.maximumHeight.Override(60f);

        cpv = gameObject.AddComponent<CustomPassVolume>();
        cpv.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
        cpv.isGlobal       = true;
        pixelPass = new PixelatePass { name = "Pixelate", pixelSize = 2, colorLevels = 32, ditherStrength = 0.35f };
        cpv.customPasses.Add(pixelPass);

        ApplyPS1Toggle();
    }

    void Update()
    {
        ApplyPS1Toggle();
        ApplyGlitch();

        // Pozlama modu + değeri her karede uygula (toggle/slider Play modunda canlı çalışsın)
        if (exposureComp != null)
        {
            if (fixedExposure)
            {
                exposureComp.mode.Override(ExposureMode.Fixed);
                exposureComp.fixedExposure.Override(fixedExposureValue);
            }
            else
            {
                exposureComp.mode.Override(ExposureMode.Automatic);
            }
        }
    }

    void ApplyPS1Toggle()
    {
        if (fogComp != null) fogComp.active = enablePS1Style;
        if (cpv     != null) cpv.enabled    = enablePS1Style;
    }

    // Bozulma = bölüm tabanı (glitchLevel) ile beden çöküşünün (1-collapse) en yükseği.
    // Hem oyun ilerledikçe hem ilaç kullandıkça görüntü dağılır.
    void ApplyGlitch()
    {
        float collapse  = StimulantSystem.Instance != null ? StimulantSystem.Instance.CollapseLevel : 1f;
        float effective = Mathf.Max(glitchLevel, 1f - collapse);

        if (pixelPass != null)
        {
            pixelPass.pixelSize   = Mathf.RoundToInt(Mathf.Lerp(2f, 4f, effective));
            pixelPass.colorLevels = Mathf.RoundToInt(Mathf.Lerp(32f, 16f, effective));
        }
        if (grain    != null) grain.intensity.Override(Mathf.Lerp(0.12f, 0.35f, effective));
        if (colorAdj != null) colorAdj.saturation.Override(Mathf.Lerp(-12f, -40f, effective));
    }
}
}
