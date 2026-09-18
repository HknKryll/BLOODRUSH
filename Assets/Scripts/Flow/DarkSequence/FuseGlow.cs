using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// Sigortanin karanlikta bulunabilmesi icin hafif, yavas nabizli bir isima. Sigorta prefab'inin
// KOKUNE eklenir: "Glow" adli parcayi HDR Unlit materyalle parlatir ve kucuk bir nokta isik
// kurar. Eldeki kopyada da calisir (SetHeld ile isigi kisilir).
//
// Materyal ve isik KODLA kurulur: HDRP Lit/isik bilesenlerini prefab YAML'inda elle yazmak
// kirilgan. Bu yuzden isima Edit modunda gorunmez, Play'de gorunur.
[DisallowMultipleComponent]
public class FuseGlow : MonoBehaviour
{
    const string LightName = "__FuseGlowLight";

    [Header("Isiyan parca")]
    [Tooltip("Bossa altindaki 'Glow' adli objenin Renderer'i kullanilir. O da yoksa sadece isik kurulur.")]
    [SerializeField] Renderer glowRenderer;
    [SerializeField] Color color = new Color(1f, 0.62f, 0.22f);   // amber
    [Tooltip("HDR carpani. Bloom ile hafif hale yapacak kadar; fazlasi karanlikta goz yorar.")]
    [SerializeField] float emissiveIntensity = 2.2f;

    [Header("Isik")]
    [SerializeField] bool    addLight    = true;
    [Tooltip("DarkSceneExposure.LightScale ile carpilir (sekansin isik konvansiyonu). CH4'te carpan " +
             "~18: 70 lumen 1200'e cikip 30 cm'deki masa esyalarini patlatiyordu. Bulunabilirligi asil " +
             "isiyan bant sagliyor; isik sadece cevresine hafif bir hale birakir.")]
    [SerializeField] float   lumen       = 12f;
    [SerializeField] float   range       = 1.8f;
    [SerializeField] Vector3 lightOffset = new Vector3(0f, 0.05f, 0f);

    [Header("Nabiz")]
    [SerializeField] float pulseSpeed = 1.6f;
    [Tooltip("Nabzin en dusuk noktasi (1 = nabiz yok).")]
    [Range(0f, 1f)]
    [SerializeField] float pulseFloor = 0.55f;

    [Header("Eldeyken")]
    [Tooltip("Oyuncunun elindeyken isik siddeti carpani — kameraya cok yakin oldugu icin kisilir.")]
    [Range(0f, 1f)]
    [SerializeField] float heldLightScale = 0.2f;

    static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");

    Material              mat;
    Light                 lamp;
    HDAdditionalLightData hd;
    bool                  held;
    float                 phase;

    public void SetHeld(bool value) => held = value;

    void Awake()
    {
        phase = Random.value * 10f;   // iki sigorta ayni anda atmasin

        if (glowRenderer == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Glow") { glowRenderer = t.GetComponent<Renderer>(); break; }
        }

        var shader = Shader.Find("HDRP/Unlit");
        if (glowRenderer != null && shader != null)
        {
            mat = new Material(shader) { name = "FuseGlow" };
            glowRenderer.sharedMaterial = mat;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (addLight) SetupLight();
        Apply(1f);
    }

    void SetupLight()
    {
        // Kopyalanan (eldeki) sigorta isigi zaten tasir — ikinci bir tane kurma.
        var existing = transform.Find(LightName);
        if (existing != null) lamp = existing.GetComponent<Light>();

        if (lamp == null)
        {
            var go = new GameObject(LightName);
            go.transform.SetParent(transform, false);
            lamp = go.AddComponent<Light>();
        }

        lamp.type  = LightType.Point;
        lamp.color = color;
        lamp.range = range;
        lamp.transform.localPosition = lightOffset;

        hd = lamp.GetComponent<HDAdditionalLightData>();
        if (hd == null) hd = lamp.gameObject.AddComponent<HDAdditionalLightData>();
        hd.EnableShadows(false);
        hd.range = range;
    }

    void Update()
    {
        float s = (Mathf.Sin((Time.time + phase) * pulseSpeed) + 1f) * 0.5f;
        Apply(Mathf.Lerp(pulseFloor, 1f, s));
    }

    void Apply(float k)
    {
        if (mat != null) mat.SetColor(UnlitColorId, color * (emissiveIntensity * k));

        if (lamp == null) return;
        float lm = lumen * k * (held ? heldLightScale : 1f) * DarkSceneExposure.LightScale;
        if (hd != null) hd.SetIntensity(lm, LightUnit.Lumen);
        else            lamp.intensity = lm;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
}
