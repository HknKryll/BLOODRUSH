using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.FX;

namespace Bloodrush.Flow
{
// Karanlik sekans icin dogru render ayarlarini TEK BASINA kurar. Sahneye bos bir objeye
// ekle, baska hicbir sey yapma — SceneVolumeSetup'taki ayarlarla ugrasmana gerek kalmaz.
//
// Neden gerekli: SceneVolumeSetup CH3 icin yazildi ve iki sey yapiyor — her yuzeye dolgu
// isigi veren bir GradientSky (ambientFill) ve otomatik pozlama. Ikisi de el feneriyle
// oynanan karanlik bir bolumun tam dusmani:
//   * Dolgu isigi karanligi yok eder, hicbir yer siyah kalmaz.
//   * Otomatik pozlama fenerin yarattigi kontrasti birebir geri alir — fener yanar ama
//     kamera parlakligi dusurdugu icin ekranda hicbir sey degismez.
//
// Bu component daha yuksek oncelikli (200) bir Volume kurup ikisini de ezer:
//   * Pozlama SABIT ve DUSUK EV — kamera duyarli olur, fener parlak okunur.
//     DIKKAT: EV YUKSELDIKCE goruntu KARARIR. 13 gunduz gibi duyarsizdir, fener kaybolur.
//   * Gokyuzu simsiyah — dolgu isigi sifir, fenerin ulasmadigi yer gercekten karanlik.
[DisallowMultipleComponent]
public class DarkSceneExposure : MonoBehaviour
{
    [Header("Pozlama")]
    [Tooltip("EV100. DUSUK = kamera duyarli = fener parlak gorunur. " +
             "8 iyi bir baslangic; 7 daha parlak, 10 daha karanlik. " +
             "12+ el feneri sahnesi icin fazla duyarsizdir.")]
    [Range(2f, 14f)]
    [SerializeField] float exposureEV = 8f;

    [Header("Isik siddeti")]
    [Tooltip("TUM isiklarin lumen degerini bu carpanla olceklendirir. EV her +1 icin kamera " +
             "2 KAT duyarsizlasir, yani EV yukseltirken isiklari da 2 KAT arttirmak gerekir. " +
             "Icerik EV 7 icin ayarlandi: EV 8 -> 2, EV 9 -> 4, EV 10 -> 8, EV 11 -> 16.")]
    [SerializeField] float lightIntensityScale = 1f;

    // Kod icinde siddet ayarlayan bilesenler (Flashlight, EmergencyLight, PowerRestoreLights,
    // DoorStatusLight) bunu okuyup carpar — boylece tek yerden butun sahne olceklenir.
    public static float LightScale { get; private set; } = 1f;

    [Header("Ortam isigi")]
    [Tooltip("Gokyuzu/dolgu isigini tamamen kes. Fenerin ulasmadigi yer simsiyah olur.")]
    [SerializeField] bool killAmbient = true;
    [Tooltip("Tam sifir cok sert geliyorsa kucuk bir taban birak (0.02-0.06 arasi).")]
    [Range(0f, 0.3f)]
    [SerializeField] float ambientFloor = 0.03f;
    [SerializeField] Color ambientTint = new Color(0.35f, 0.42f, 0.55f);

    [Header("PS1 stili")]
    [Tooltip("Pixelate + grain + renk kirpma. SceneVolumeSetup'taki gorunumun aynisi, " +
             "o bilesen bu sahnede olmadigi icin buraya tasindi.")]
    [SerializeField] bool  enablePS1      = true;
    [SerializeField] int   pixelSize      = 2;
    [SerializeField] int   colorLevels    = 32;
    [Range(0f, 1f)]
    [SerializeField] float ditherStrength = 0.35f;
    [Tooltip("Bolum tabani bozulma. SceneVolumeSetup notu: Ch1-2: 0, Ch3: 0.25, " +
             "Ch4: 0.5, Ch5: 0.8, Ch6: 1.")]
    [Range(0f, 1f)]
    [SerializeField] float glitchLevel    = 0.5f;

    [Header("Renk")]
    [SerializeField] bool  colorGrade = true;
    [SerializeField] float contrast   = 6f;
    [SerializeField] float saturation = -20f;
    [Tooltip("Fenerin isigini hafifce dagitir — karanlikta hos durur.")]
    [SerializeField] bool  bloom      = true;
    [Tooltip("Bu parlakligin USTUNDEKI yuzeyler hale yapar. DUSUK = her acik yuzey parlar. " +
             "Fener beyaz bir yuzeye yakindan vurdugunda patlama gibi gorunuyorsa BUNU YUKSELT. " +
             "0.9 cok dusuktu; 1.4-2.0 arasi bu sahne icin daha dogru.")]
    [Range(0.1f, 4f)]
    [SerializeField] float bloomThreshold = 1.4f;
    [Tooltip("Halenin gucu. 0 = hale yok.")]
    [Range(0f, 3f)]
    [SerializeField] float bloomIntensity = 0.7f;

    [Header("Sis")]
    [Tooltip("Acikken mesafeyi yutan yogun sis — karanligi derinlestirir.")]
    [SerializeField] bool  overrideFog = true;
    [Tooltip("KUCUK = daha yogun sis.")]
    [SerializeField] float fogMeanFreePath = 35f;

    Exposure         exposureComp;
    PixelatePass     pixelPass;
    CustomPassVolume cpv;
    FilmGrain        grain;
    ColorAdjustments colorAdj;
    Bloom            bloomComp;

    void Awake()
    {
        LightScale = Mathf.Max(0.01f, lightIntensityScale);
        ScaleExistingLights();

        var vol      = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 200f;   // SceneVolumeSetup (100) uzerinde kalmali

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        exposureComp = profile.Add<Exposure>();
        exposureComp.active = true;
        exposureComp.mode.Override(ExposureMode.Fixed);
        exposureComp.fixedExposure.Override(exposureEV);

        if (colorGrade)
        {
            colorAdj = profile.Add<ColorAdjustments>();
            colorAdj.active = true;
            colorAdj.contrast.Override(contrast);
            colorAdj.saturation.Override(saturation);
        }

        if (bloom)
        {
            bloomComp = profile.Add<Bloom>();
            bloomComp.active = true;
            bloomComp.threshold.Override(bloomThreshold);
            bloomComp.intensity.Override(bloomIntensity);
            bloomComp.scatter.Override(0.7f);
        }

        grain = profile.Add<FilmGrain>();
        grain.active = true;
        grain.intensity.Override(0.12f);
        grain.response.Override(0.85f);

        if (killAmbient)
        {
            var env = profile.Add<VisualEnvironment>();
            env.skyType.Override((int)SkyType.Gradient);

            var sky = profile.Add<GradientSky>();
            Color c = ambientTint * ambientFloor;
            sky.top.Override(c);
            sky.middle.Override(c);
            sky.bottom.Override(c);
            sky.multiplier.Override(1f);
        }

        if (overrideFog)
        {
            var fog = profile.Add<Fog>();
            fog.active = true;
            fog.enabled.Override(true);
            fog.meanFreePath.Override(fogMeanFreePath);
        }

        // PS1: AfterPostProcess'e pixelate custom pass — SceneVolumeSetup ile ayni kurulum.
        cpv = gameObject.AddComponent<CustomPassVolume>();
        cpv.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
        cpv.isGlobal       = true;
        pixelPass = new PixelatePass
        {
            name           = "Pixelate",
            pixelSize      = pixelSize,
            colorLevels    = colorLevels,
            ditherStrength = ditherStrength,
        };
        cpv.customPasses.Add(pixelPass);
        ApplyLook();

        Debug.Log($"[DarkSceneExposure] Isik carpani x{LightScale:0.##}. Kuruldu — EV {exposureEV}, ambient {(killAmbient ? ambientFloor.ToString("0.00") : "dokunulmadi")}, " +
                  $"sis {(overrideFog ? fogMeanFreePath.ToString("0") : "dokunulmadi")}.", this);
    }
    // Play modunda slider'lari canli denemek icin — kullanici bu bakisi cok kez ince
    // ayarladi, her seferinde Play'den cikmak zorunda kalmasin.
    void Update() => ApplyLook();

    void ApplyLook()
    {
        if (exposureComp != null) exposureComp.fixedExposure.Override(exposureEV);

        // Canli ayarlanabilsin: beyaz yuzeylerin patlamasi bu iki degerle ayarlaniyor.
        if (bloomComp != null)
        {
            bloomComp.threshold.Override(bloomThreshold);
            bloomComp.intensity.Override(bloomIntensity);
        }
        if (cpv          != null) cpv.enabled = enablePS1;

        if (pixelPass != null)
        {
            // Bozulma arttikca piksel buyur, renk sayisi duser — SceneVolumeSetup mantigi.
            pixelPass.pixelSize      = Mathf.RoundToInt(Mathf.Lerp(pixelSize, pixelSize * 2f, glitchLevel));
            pixelPass.colorLevels    = Mathf.RoundToInt(Mathf.Lerp(colorLevels, colorLevels * 0.5f, glitchLevel));
            pixelPass.ditherStrength = ditherStrength;
        }
        if (grain    != null) grain.intensity.Override(Mathf.Lerp(0.12f, 0.35f, glitchLevel));
        if (colorAdj != null) colorAdj.saturation.Override(Mathf.Lerp(saturation, saturation - 20f, glitchLevel));
    }

    // Sahnede HAZIR duran isiklar (builder tavan isiklari vb.) — bunlar kod icinde siddet
    // ayarlamadigi icin burada bir kez olceklenir.
    void ScaleExistingLights()
    {
        if (Mathf.Approximately(LightScale, 1f)) return;

        int n = 0;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hd = l.GetComponent<HDAdditionalLightData>();
            if (hd == null) continue;
            hd.SetIntensity(hd.intensity * LightScale, hd.lightUnit);
            n++;
        }
        Debug.Log($"[DarkSceneExposure] {n} mevcut isik x{LightScale:0.##} olceklendi.", this);
    }
}
}
