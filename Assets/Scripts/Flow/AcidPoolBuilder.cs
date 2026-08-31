using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Asit havuzu: "boş zemine düşüp ölme"yi görselleştirir (parkur çukuru gibi) —
// parlayan asit yüzeyi + kenar çerçevesi + kabarcıklar + kızıl ışık + KillVolume
// (düşen oyuncu son checkpoint'te canlanır — proje standardı). GravityTower'daki
// dip-tehlike deseninin havuz versiyonu.
//
// Kullanım: boş GameObject'i çukurun ORTASINA koy (objenin Y'si = asit yüzeyi),
// bu script'i ekle, Width/Depth'i çukuru kaplayacak şekilde ayarla →
// ⋮ "Asit Havuzu Kur". Tekrar çalıştırınca kendi çocuklarını yenileyerek kurar.
namespace Bloodrush.Flow
{
public class AcidPoolBuilder : MonoBehaviour
{
    [Header("Boyut (metre)")]
    [SerializeField] float width = 20f;
    [SerializeField] float depth = 12f;

    [Header("Görünüm")]
    [Tooltip("Asit rengi. Kırmızı varsayılan; yeşil asit için (0.3, 1, 0.15) gibi.")]
    [SerializeField] Color acidColor   = new Color(1f, 0.12f, 0.06f);
    [Tooltip("Parlaklık çarpanı (HDR → Bloom yakalar).")]
    [SerializeField] float glowMult    = 3f;
    [SerializeField] int   bubbleCount = 16;
    [SerializeField] int   seed        = 777;
    [Tooltip("Havuzun üstüne alçak kızıl ışıklar koy (duvarlara vursun).")]
    [SerializeField] bool  castLight   = true;

    [Header("Öldürme")]
    [Tooltip("Yüzeyden itibaren bu yükseklikteki bölge öldürür (KillVolume).")]
    [SerializeField] float killHeight = 1.2f;

    [ContextMenu("Asit Havuzu Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var surfMat = UnlitMat(acidColor * glowMult);
        var brightMat = UnlitMat(acidColor * (glowMult * 1.8f));
        var rimMat = new Material(Shader.Find("HDRP/Lit"));
        rimMat.SetColor("_BaseColor", new Color(0.08f, 0.05f, 0.05f));
        rimMat.SetFloat("_Smoothness", 0.1f);

        // Asit yüzeyi — collider'sız, içine düşülür
        MakeBox("AsitYuzeyi", new Vector3(0f, -0.06f, 0f), new Vector3(width, 0.12f, depth), surfMat, false);

        // Kenar çerçevesi (havuz ağzı okunsun) — collider'lı, üstünde durulabilir
        float t = 0.45f, rimH = 0.35f;
        MakeBox("Kenar_K", new Vector3(0f, rimH * 0.5f - 0.06f,  depth * 0.5f + t * 0.5f), new Vector3(width + t * 2f, rimH, t), rimMat, true);
        MakeBox("Kenar_G", new Vector3(0f, rimH * 0.5f - 0.06f, -depth * 0.5f - t * 0.5f), new Vector3(width + t * 2f, rimH, t), rimMat, true);
        MakeBox("Kenar_D", new Vector3( width * 0.5f + t * 0.5f, rimH * 0.5f - 0.06f, 0f), new Vector3(t, rimH, depth), rimMat, true);
        MakeBox("Kenar_B", new Vector3(-width * 0.5f - t * 0.5f, rimH * 0.5f - 0.06f, 0f), new Vector3(t, rimH, depth), rimMat, true);

        // Kabarcıklar — yüzeyde yarı batık küçük küreler (daha parlak ton)
        var rng = new System.Random(seed);
        for (int i = 0; i < bubbleCount; i++)
        {
            float bx = ((float)rng.NextDouble() - 0.5f) * (width - 1.5f);
            float bz = ((float)rng.NextDouble() - 0.5f) * (depth - 1.5f);
            float s  = 0.15f + (float)rng.NextDouble() * 0.35f;
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "Kabarcik";
            DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(transform, false);
            b.transform.localPosition = new Vector3(bx, s * 0.25f, bz);   // yarı batık
            b.transform.localScale    = Vector3.one * s;
            b.GetComponent<Renderer>().sharedMaterial = brightMat;
            b.isStatic = true;
        }

        // Alçak kızıl ışıklar — asit parıltısı duvarlara vursun (~8m aralık)
        if (castLight)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(width / 8f));
            int nz = Mathf.Max(1, Mathf.RoundToInt(depth / 8f));
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var lgo = new GameObject($"AsitIsik_{ix}_{iz}");
                lgo.transform.SetParent(transform, false);
                lgo.transform.localPosition = new Vector3(
                    -width * 0.5f + width * (ix + 0.5f) / nx,
                    0.8f,
                    -depth * 0.5f + depth * (iz + 0.5f) / nz);
                var light   = lgo.AddComponent<Light>();
                light.type  = LightType.Point;
                light.color = acidColor;
                light.range = 9f;
                var hd = lgo.AddComponent<HDAdditionalLightData>();
                hd.SetIntensity(1500f, LightUnit.Lumen);
                hd.EnableShadows(false);
            }
        }

        // KillVolume — yüzeyin üstünde killHeight kadar bölge öldürür (checkpoint respawn)
        var kv = new GameObject("KillVolume");
        kv.transform.SetParent(transform, false);
        kv.transform.localPosition = new Vector3(0f, killHeight * 0.5f - 0.06f, 0f);
        var box = kv.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width, killHeight, depth);
        kv.AddComponent<KillVolume>();
    }

    GameObject MakeBox(string name, Vector3 localPos, Vector3 size, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) { var c = go.GetComponent<Collider>(); if (c) DestroyImmediate(c); }
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        go.isStatic = true;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    Material UnlitMat(Color hdr)
    {
        var m = new Material(Shader.Find("HDRP/Unlit"));
        m.SetColor("_UnlitColor", hdr);
        return m;
    }
}
}
