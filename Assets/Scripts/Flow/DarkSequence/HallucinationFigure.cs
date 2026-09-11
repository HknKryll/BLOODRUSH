using UnityEngine;

namespace Bloodrush.Flow
{
// Karanlik sekansta beliren HALUSINASYON figuru (oyuncunun karisi) — primitive'lerden
// kurulan kadin oranli bir siluet. Projede sivil/kadin modeli olmadigi icin greybox
// builder deseniyle uretiliyor (LabShelfBuilder / DarkSequenceBuilder ile ayni yapi).
//
// Bos bir objeye ekle, sonra dis menusunden "Halusinasyon Figurunu Kur".
// Sonra bu objeyi SilhouetteScare'in "Silhouette" alanina surukle.
//
// MALZEME KARARI — figur SIMSIYAH DEGIL: zifiri karanlik bir odada siyah bir figur hic
// gorunmez. HDRP/Unlit ve sonuk gri-mavi kullaniliyor; kendi kendine hafifce okunan,
// fenerin uzerine dusmesine ihtiyac duymayan bir sekil. Fener tutuldugunda da parlamiyor
// (unlit), yani "isik tuttum ama hala ayni" hissi veriyor — halusinasyon icin dogrusu bu.
//
// Kadin siluetini okutan sey yuz detayi degil, HAT: uzun sac kutlesi, dar bel, kalca
// genisligi ve etek konisi. Bacak yerine etek kullanmak siluette cok daha net okunuyor.
public class HallucinationFigure : MonoBehaviour
{
    [Header("Olcu")]
    [Tooltip("Toplam boy (m). 1.60-1.70 arasi dogal durur.")]
    [SerializeField] float height = 1.66f;

    [Header("Gorunum")]
    [Tooltip("Siluet rengi. KOYU ama SIYAH DEGIL — karanlikta secilebilmeli.")]
    [SerializeField] Color figureColor = new Color(0.20f, 0.22f, 0.28f);
    [Tooltip("Parlaklik carpani. Yukseltirsen karanlikta daha net, dusurursen daha silik.")]
    [Range(0.2f, 4f)]
    [SerializeField] float brightness = 1f;
    [Tooltip("Sac rengi biraz daha koyu olsun — hat okunsun.")]
    [SerializeField] Color hairColor = new Color(0.12f, 0.13f, 0.17f);

    [Header("Silue hatti")]
    [Tooltip("Sac uzunlugu (boy orani). Uzun sac kadin siluetinin ana ipucu.")]
    [Range(0.05f, 0.35f)]
    [SerializeField] float hairLength = 0.22f;
    [Tooltip("Etek genisligi (boy orani). Buyuk = daha belirgin kadin hatti.")]
    [Range(0.08f, 0.3f)]
    [SerializeField] float skirtWidth = 0.17f;
    [Tooltip("Kollar govdeye yapisik mi dursun (daha sakin/urkutucu) yoksa hafif acik mi.")]
    [Range(0f, 25f)]
    [SerializeField] float armSpread = 7f;

    Material bodyMat, hairMat;

    [ContextMenu("Halusinasyon Figurunu Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        bodyMat = Unlit(figureColor * brightness);
        hairMat = Unlit(hairColor  * brightness);

        float h = height;

        // Oranlar boya gore — boyu degistirince figur bozulmasin.
        float headR   = h * 0.052f;
        float headY   = h * 0.93f;
        float neckY   = h * 0.855f;
        float chestY  = h * 0.72f;
        float waistY  = h * 0.60f;
        float hipY    = h * 0.52f;
        float skirtY  = h * 0.34f;
        float legY    = h * 0.16f;

        var root = new GameObject("Figur").transform;
        root.SetParent(transform, false);

        // Bas + boyun
        Sph(root, "Bas",   new Vector3(0f, headY, 0f), Vector3.one * headR * 2f, bodyMat);
        Cyl(root, "Boyun", new Vector3(0f, neckY, 0f), new Vector3(h * 0.026f, h * 0.035f, h * 0.026f), bodyMat);

        // Sac: basin arkasini ve omuzlara dusen kutleyi kaplar — siluetin kadin okunmasini
        // saglayan asil parca.
        Sph(root, "SacUst", new Vector3(0f, headY + h * 0.012f, -h * 0.008f),
            new Vector3(headR * 2.25f, headR * 2.2f, headR * 2.35f), hairMat);
        Cyl(root, "SacArka", new Vector3(0f, headY - hairLength * h * 0.5f, -headR * 0.75f),
            new Vector3(headR * 1.9f, hairLength * h * 0.5f, headR * 1.1f), hairMat);

        // Govde: omuz -> dar bel -> kalca. Uc ayri parca konikligi ucuza tasiyor.
        Cyl(root, "Omuz",  new Vector3(0f, chestY, 0f), new Vector3(h * 0.105f, h * 0.075f, h * 0.062f), bodyMat);
        Cyl(root, "Bel",   new Vector3(0f, waistY, 0f), new Vector3(h * 0.072f, h * 0.065f, h * 0.05f),  bodyMat);
        Cyl(root, "Kalca", new Vector3(0f, hipY,   0f), new Vector3(h * 0.108f, h * 0.05f,  h * 0.07f),  bodyMat);

        // Etek: asagi dogru genisleyen koni hissi. Silindiri hafif buyuterek yaklasiyoruz.
        Cyl(root, "Etek",     new Vector3(0f, skirtY, 0f),
            new Vector3(skirtWidth * h, h * 0.115f, skirtWidth * h * 0.75f), bodyMat);
        Cyl(root, "EtekUcu",  new Vector3(0f, skirtY - h * 0.10f, 0f),
            new Vector3(skirtWidth * h * 1.18f, h * 0.02f, skirtWidth * h * 0.9f), bodyMat);

        // Bacaklar: eteğin altindan cikan ince hat
        Cyl(root, "BacakSol", new Vector3(-h * 0.035f, legY, 0f), new Vector3(h * 0.028f, h * 0.14f, h * 0.028f), bodyMat);
        Cyl(root, "BacakSag", new Vector3( h * 0.035f, legY, 0f), new Vector3(h * 0.028f, h * 0.14f, h * 0.028f), bodyMat);

        // Kollar: govdeye yakin, hafif acili
        Arm(root, "KolSol", -1f, h, chestY);
        Arm(root, "KolSag",  1f, h, chestY);
    }

    void Arm(Transform parent, string name, float side, float h, float chestY)
    {
        var arm = new GameObject(name).transform;
        arm.SetParent(parent, false);
        arm.localPosition = new Vector3(side * h * 0.098f, chestY - h * 0.02f, 0f);
        arm.localRotation = Quaternion.Euler(0f, 0f, -side * armSpread);

        Cyl(arm, "Ust", new Vector3(0f, -h * 0.075f, 0f), new Vector3(h * 0.026f, h * 0.08f, h * 0.026f), bodyMat);
        Cyl(arm, "Alt", new Vector3(0f, -h * 0.205f, 0f), new Vector3(h * 0.022f, h * 0.07f, h * 0.022f), bodyMat);
    }

    // ── Yardimcilar (proje konvansiyonu: her builder kendi kopyasini tasir) ──

    void Cyl(Transform p, string n, Vector3 pos, Vector3 scale, Material m)
        => Prim(p, n, PrimitiveType.Cylinder, pos, scale, m);

    void Sph(Transform p, string n, Vector3 pos, Vector3 scale, Material m)
        => Prim(p, n, PrimitiveType.Sphere, pos, scale, m);

    void Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Collider YOK: halusinasyon fiziksel degil, oyuncu icinden gecebilmeli.
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }

    // Unlit + TRANSPARENT: SilhouetteScare alpha uzerinden soldurdugu icin materyal
    // saydamligi desteklemek zorunda. HDRP/Unlit'te Surface Type'i koda ayarliyoruz.
    Material Unlit(Color c)
    {
        var m = new Material(Shader.Find("HDRP/Unlit"));
        m.SetColor("_UnlitColor", c);

        m.SetFloat("_SurfaceType", 1f);            // 1 = Transparent
        m.SetFloat("_BlendMode", 0f);              // Alpha
        m.SetFloat("_DstBlend", 10f);              // OneMinusSrcAlpha
        m.SetFloat("_SrcBlend", 5f);               // SrcAlpha
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_AlphaCutoffEnable", 0f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_BLENDMODE_ALPHA");
        m.DisableKeyword("_ALPHATEST_ON");

        return m;
    }
}
}
