using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// Bir grup tavan lambasini birlikte yanip sondurur — hem Light'lari hem de PANEL
// MESH'lerinin emissive rengini.
//
// Neden panel de gerekli: DarkSequenceBuilder her lambaya bir de HDRP/Unlit parlak panel
// kuruyor. Unlit materyal kendi kendine parladigi icin Light kapatilsa bile panel zifiri
// karanlikta parlayan bir dikdortgen olarak kaliyordu — isik vermeyen ama isik gibi duran
// bir sey. Ikisini birlikte surmek zorunlu.
//
// Kullanim: lambalarin bagli oldugu KOK objeyi (or. E_ValfOdasiA/Isiklar) Lights Root'a
// surukle, gerisi otomatik.
public class FlickerLightGroup : MonoBehaviour
{
    [Header("Kaynak")]
    [Tooltip("Lambalarin ve panellerin bulundugu kok obje. Altindaki tum Light ve Renderer " +
             "bilesenleri otomatik toplanir.")]
    [SerializeField] Transform lightsRoot;

    [Header("Yanip sonme")]
    [Tooltip("Yanik kaldigi sure araligi (min, max sn).")]
    [SerializeField] Vector2 onRange  = new Vector2(0.8f, 2.4f);
    [Tooltip("Sonuk kaldigi sure araligi (min, max sn). Kisa tut — 'kontak var' hissi.")]
    [SerializeField] Vector2 offRange = new Vector2(0.06f, 0.35f);
    [Tooltip("Sonmeden once kac kez hizlica kirpissin (0 = duz ac/kapa).")]
    [SerializeField] Vector2Int stutter = new Vector2Int(0, 3);
    [Tooltip("Yanikken lumen degeri.")]
    [SerializeField] float litLumen = 1100f;

    [Header("Panel")]
    [Tooltip("Panel mesh'inin sonukken karartilma orani. 0 = tamamen kararir.")]
    [Range(0f, 1f)]
    [SerializeField] float panelDimFactor = 0.04f;

    Light[]                 lights;
    HDAdditionalLightData[] hd;
    Renderer[]              panels;
    MaterialPropertyBlock   mpb;
    Color[]                 panelBaseColors;

    static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        var root = lightsRoot != null ? lightsRoot : transform;

        lights = root.GetComponentsInChildren<Light>(true);
        panels = root.GetComponentsInChildren<Renderer>(true);
        mpb    = new MaterialPropertyBlock();

        if (lights.Length == 0 && panels.Length == 0)
        {
            Debug.LogWarning("[FlickerLightGroup] Lights Root altinda isik/panel bulunamadi.", this);
            enabled = false;
            return;
        }

        hd = new HDAdditionalLightData[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            hd[i] = lights[i].GetComponent<HDAdditionalLightData>();
            if (hd[i] == null) hd[i] = lights[i].gameObject.AddComponent<HDAdditionalLightData>();
            hd[i].EnableShadows(false);   // kirpisan golge hem pahali hem gurultulu
        }

        // Panellerin ozgun rengini sakla — sondurup geri yakabilmek icin.
        panelBaseColors = new Color[panels.Length];
        for (int i = 0; i < panels.Length; i++)
        {
            var m = panels[i] != null ? panels[i].sharedMaterial : null;
            panelBaseColors[i] = m == null ? Color.white
                               : m.HasProperty(UnlitColorId) ? m.GetColor(UnlitColorId)
                               : m.HasProperty(BaseColorId)  ? m.GetColor(BaseColorId)
                               : Color.white;
        }
    }

    void OnEnable()  => StartCoroutine(Loop());
    void OnDisable() => StopAllCoroutines();

    IEnumerator Loop()
    {
        while (true)
        {
            SetOn(true);
            yield return new WaitForSeconds(Random.Range(onRange.x, onRange.y));

            // Sonmeden once birkac hizli kirpisma — duz ac/kapa'dan cok daha "bozuk" durur.
            int n = Random.Range(stutter.x, stutter.y + 1);
            for (int i = 0; i < n; i++)
            {
                SetOn(false);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
                SetOn(true);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
            }

            SetOn(false);
            yield return new WaitForSeconds(Random.Range(offRange.x, offRange.y));
        }
    }

    void SetOn(bool on)
    {
        float lumen = litLumen * DarkSceneExposure.LightScale;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            if (hd[i] != null) hd[i].SetIntensity(lumen, LightUnit.Lumen);
            lights[i].enabled = on;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null) continue;
            Color c = on ? panelBaseColors[i] : panelBaseColors[i] * panelDimFactor;
            c.a = panelBaseColors[i].a;

            panels[i].GetPropertyBlock(mpb);
            var m = panels[i].sharedMaterial;
            if (m != null && m.HasProperty(UnlitColorId)) mpb.SetColor(UnlitColorId, c);
            else                                          mpb.SetColor(BaseColorId,  c);
            panels[i].SetPropertyBlock(mpb);
        }
    }
}
}
