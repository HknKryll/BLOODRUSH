using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Bloodrush.FX;

namespace Bloodrush.Flow
{
// Sahne giydirici v2 — KONUMDAN BAĞIMSIZ. Bu objenin NEREDE durduğu önemsizdir:
// sahnedeki İSİMLİ yüzeylerin kendi bounds'ları kullanılır.
//  • "Tavan Işığı Diz"  → adı tavan/ceiling/kapak içeren HER yüzeyin altına grid ışık+panel.
//  • "Prop Serp"        → adı zemin/floor/doseme içeren HER yüzeyin üstüne prop.
//  • "Materyalleri Uygula" → isim eşleşmesiyle palet materyali atar (rebuild yok, Undo'lu).
// (v1'deki dünya-uzayı kutu + raycast kaldırıldı — konumlandırma hatasına çok açıktı.)
//
// Kullanım: sahneye boş bir GameObject koy, bu script'i ekle, SurfacePalette ata,
// ⋮ menüsünden komutları çalıştır. Root boşsa TÜM sahne işlenir.
public class SceneDresser : MonoBehaviour
{
    const string PropContainer  = "SahneProplari";
    const string LightContainer = "SahneIsiklari";

    [Header("Palet")]
    [Tooltip("Bu bölümün SurfacePalette asset'i (Create → Bloodrush → Yüzey Paleti).")]
    [SerializeField] SurfacePalette palette;
    [Tooltip("Boş = TÜM sahne (önerilen). Doldurulursa sadece bu objenin ALTI işlenir.")]
    [SerializeField] Transform root;

    [Header("Güvenlik")]
    [Tooltip("AÇIK (önerilen): sadece varsayılan gri materyalli yüzeyler değişir — kendi materyallerin korunur.")]
    [SerializeField] bool onlyReplaceDefault = true;
    [Tooltip("Bu kelimeleri İÇEREN objelere materyal atanmaz (emissive panel, ışık, bariyer, ekran...).")]
    [SerializeField] string[] skipKeywords =
    {
        "panel", "isik", "ışık", "glow", "emissive", "aksan", "diken",
        "barrier", "bariyer", "trigger", "logo", "ekran", "amblem", "serit", "şerit",
        "hali", "halı", "bitki", "yaprak", "govde", "gövde"
    };

    [Header("Tavan Işığı (tavan yüzeylerinin altına dizilir)")]
    [Tooltip("Işıklar arası hedef mesafe (m). 6-8 iyi — arada karanlık kalsın ki 'ışık havuzu' olsun.")]
    [SerializeField] float   lightSpacing   = 7f;
    [SerializeField] float   lightLumen     = 2600f;
    [SerializeField] float   lightRange     = 13f;
    [Tooltip("Işık, tavan alt yüzünden bu kadar aşağı konur.")]
    [SerializeField] float   lightDrop      = 0.5f;
    [Tooltip("Işığın nereden geldiği belli olsun diye tavana parlak panel koy.")]
    [SerializeField] bool    makeLightPanel = true;
    [SerializeField] Vector3 panelSize      = new Vector3(3f, 0.14f, 1.4f);
    [Tooltip("Bundan küçük tavan parçalarına ışık dizilmez (m²) — aksan/lento gibi kırıntılar atlanır.")]
    [SerializeField] float   minCeilingArea = 4f;

    [Header("Prop (zemin yüzeylerinin üstüne serpilir)")]
    [Tooltip("Yoğunluk: her 25 m² zemine kaç prop. 1-2 az, 3-4 dolu.")]
    [SerializeField] float propDensity  = 1.5f;
    [Tooltip("Zemin başına üst sınır (dev zeminler boğulmasın).")]
    [SerializeField] int   maxPropsPerFloor = 30;
    [Tooltip("Bundan küçük zemin parçalarına prop konmaz (m²).")]
    [SerializeField] float minFloorArea = 8f;
    [SerializeField] int   propSeed     = 12345;

    // ───────────────────────── Materyal ─────────────────────────

    [ContextMenu("1) Materyalleri Uygula")]
    void ApplyMaterials()
    {
#if UNITY_EDITOR
        if (palette == null) { Debug.LogWarning("[SceneDresser] Palet atanmadı.", this); return; }

        int changed = 0, alreadySet = 0, skipListed = 0;
        foreach (var r in AllRenderers())
        {
            string n = r.gameObject.name;
            if (IsSkipped(n)) { skipListed++; continue; }

            var mat = PickMaterial(n);
            if (mat == null) continue;                       // isim eşleşmedi — dokunma

            if (onlyReplaceDefault && !IsDefaultMaterial(r.sharedMaterial)) { alreadySet++; continue; }

            UnityEditor.Undo.RecordObject(r, "Materyal Uygula");
            r.sharedMaterial = mat;
            UnityEditor.EditorUtility.SetDirty(r);
            changed++;
        }

        Debug.Log($"[SceneDresser] Materyal: {changed} gri yüzey giydirildi · " +
                  $"{alreadySet} yüzey zaten palet/özel materyalde (dokunulmadı) · " +
                  $"{skipListed} skip-listesinde (panel/ışık/ekran vb). Ctrl+Z geri alır.", this);
#endif
    }

    Material PickMaterial(string rawName)
    {
        string n = rawName.ToLowerInvariant();
        if (Has(n, "duvar", "wall", "kanat", "lento"))                     return palette.wallMat;
        if (Has(n, "zemin", "doseme", "döşeme", "floor"))                  return palette.floorMat;
        if (Has(n, "tavan", "ceiling", "kapak"))                           return palette.ceilingMat;
        if (Has(n, "basamak", "merdiven", "stair"))                        return palette.stairMat;
        if (Has(n, "kolon", "raf", "kopru", "köprü", "ledge", "platform")) return palette.metalMat;
        return null;
    }

#if UNITY_EDITOR
    // Kaçış kapısı: koruma ("zaten materyali var") yüzünden atlanan yüzeyler için.
    // Hierarchy'de objeleri SEÇ → bu komut. Seçilenlerde (ve çocuklarında) isim eşleşen
    // her yüzeye paleti ZORLA uygular — onlyReplaceDefault ve skip listesi YOK SAYILIR.
    [ContextMenu("5) SEÇİLİ Objelere Zorla Uygula")]
    void ForceApplySelection()
    {
        if (palette == null) { Debug.LogWarning("[SceneDresser] Palet atanmadı.", this); return; }

        var sel = UnityEditor.Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            Debug.LogWarning("[SceneDresser] Hiçbir obje seçili değil — önce Hierarchy'de duvar/zemin seç.", this);
            return;
        }

        int changed = 0, noMatch = 0;
        foreach (var go in sel)
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mat = PickMaterial(r.gameObject.name);
            if (mat == null) { noMatch++; continue; }        // isim hiçbir türe uymuyor

            UnityEditor.Undo.RecordObject(r, "Materyal Zorla Uygula");
            r.sharedMaterial = mat;
            UnityEditor.EditorUtility.SetDirty(r);
            changed++;
        }

        Debug.Log($"[SceneDresser] Zorla: {changed} yüzeye uygulandı · {noMatch} isim eşleşmedi " +
                  "(duvar/zemin/tavan/basamak/kolon kelimeleri yok). Ctrl+Z geri alır.", this);
    }
#endif

    // ───────────────────────── Mevcut ışık rengi + atmosfer ─────────────────────────

    // DİKKAT: Bu komut ışık OLUŞTURMAZ — var olanları renklendirir + ambient/sis ayarlar.
    // Işığı olmayan alan için "3) Tavan Işığı Diz".
    [ContextMenu("2) Mevcut Işıkları Renklendir + Atmosfer")]
    void ApplyLights()
    {
#if UNITY_EDITOR
        if (palette == null) { Debug.LogWarning("[SceneDresser] Palet atanmadı.", this); return; }

        var lights = root != null ? root.GetComponentsInChildren<Light>(true)
                                  : FindObjectsOfType<Light>(true);
        int n = 0;
        foreach (var l in lights)
        {
            if (l == null || l.type == LightType.Directional) continue;

            if (palette.applyLightColor)
            {
                UnityEditor.Undo.RecordObject(l, "Işık Uygula");
                l.color = palette.lightColor;
                UnityEditor.EditorUtility.SetDirty(l);
            }
            if (palette.applyLightIntensity && l.TryGetComponent(out HDAdditionalLightData hd))
            {
                UnityEditor.Undo.RecordObject(hd, "Işık Uygula");
                hd.SetIntensity(palette.lightIntensityLumen, LightUnit.Lumen);
                UnityEditor.EditorUtility.SetDirty(hd);
            }
            n++;
        }

        int atmo = 0;
        if (palette.applyAtmosphere)
        {
            foreach (var svs in FindObjectsOfType<SceneVolumeSetup>(true))
            {
                var so = new UnityEditor.SerializedObject(svs);
                SetProp(so, "ambientColor",     palette.ambientColor);
                SetProp(so, "ambientIntensity", palette.ambientIntensity);
                SetProp(so, "fogMeanFreePath",  palette.fogMeanFreePath);
                so.ApplyModifiedProperties();
                atmo++;
            }
        }

        Debug.Log($"[SceneDresser] {n} mevcut ışık renklendirildi · {atmo} SceneVolumeSetup güncellendi " +
                  "(ambient/sis Play modunda görünür). " +
                  (n == 0 ? "0 ışık: bu komut ışık OLUŞTURMAZ — '3) Tavan Işığı Diz' kullan." : ""), this);
#endif
    }

#if UNITY_EDITOR
    static void SetProp(UnityEditor.SerializedObject so, string name, Color v)
    { var p = so.FindProperty(name); if (p != null) p.colorValue = v; }
    static void SetProp(UnityEditor.SerializedObject so, string name, float v)
    { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
#endif

    // ───────────────────────── Tavan ışığı dizme (bounds-tabanlı) ─────────────────────────

    [ContextMenu("3) Tavan Işığı Diz")]
    void PlaceCeilingLights()
    {
        var parent = FreshContainer(LightContainer);

        Color col = palette != null ? palette.lightColor : new Color(0.9f, 0.85f, 0.75f);
        Material panelMat = null;
        if (makeLightPanel)
        {
            panelMat = new Material(Shader.Find("HDRP/Unlit"));
            panelMat.SetColor("_UnlitColor", col * 2.2f);   // HDR → Bloom yakalasın
        }

        int ceilings = 0, lightsPlaced = 0;
        var report = new List<string>();

        foreach (var r in AllRenderers())
        {
            string n = r.gameObject.name.ToLowerInvariant();
            if (!Has(n, "tavan", "ceiling", "kapak")) continue;

            Bounds b = r.bounds;                                  // dünya AABB — konum bağımsız
            float area = b.size.x * b.size.z;
            if (area < minCeilingArea) continue;                  // aksan/kırıntı parçaları atla

            int nx = Mathf.Max(1, Mathf.RoundToInt(b.size.x / Mathf.Max(0.5f, lightSpacing)));
            int nz = Mathf.Max(1, Mathf.RoundToInt(b.size.z / Mathf.Max(0.5f, lightSpacing)));

            int before = lightsPlaced;
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = Mathf.Lerp(b.min.x, b.max.x, (ix + 0.5f) / nx);
                float z = Mathf.Lerp(b.min.z, b.max.z, (iz + 0.5f) / nz);
                float underside = b.min.y;                        // tavanın alt yüzü

                if (makeLightPanel)
                {
                    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panel.name = "IsikPanel";
                    DestroyImmediate(panel.GetComponent<Collider>());
                    panel.transform.SetParent(parent, false);
                    panel.transform.position   = new Vector3(x, underside - panelSize.y * 0.5f - 0.02f, z);
                    panel.transform.localScale = panelSize;
                    panel.GetComponent<Renderer>().sharedMaterial = panelMat;
                    panel.isStatic = true;
                }

                var lgo = new GameObject("Isik");
                lgo.transform.SetParent(parent, false);
                lgo.transform.position = new Vector3(x, underside - lightDrop, z);
                var light   = lgo.AddComponent<Light>();
                light.type  = LightType.Point;
                light.color = col;
                light.range = lightRange;
                var hd = lgo.AddComponent<HDAdditionalLightData>();
                hd.SetIntensity(lightLumen, LightUnit.Lumen);
                hd.EnableShadows(false);
                lightsPlaced++;
            }

            ceilings++;
            report.Add($"{r.gameObject.name}: {lightsPlaced - before}");
        }

        Debug.Log(lightsPlaced == 0
            ? "[SceneDresser] 0 ışık: sahnede adı Tavan/Ceiling/Kapak içeren (ve ≥" + minCeilingArea +
              " m²) yüzey bulunamadı. Root doluysa boşalt ya da tavan objelerinin adını kontrol et."
            : $"[SceneDresser] {ceilings} tavana toplam {lightsPlaced} ışık dizildi ('{LightContainer}'). " +
              $"Detay: {string.Join(" · ", report)}", this);
    }

    // ───────────────────────── Prop serpme (bounds-tabanlı) ─────────────────────────

    [ContextMenu("4) Prop Serp")]
    void ScatterProps()
    {
        var parent = FreshContainer(PropContainer);

        Material propMat = palette != null && palette.metalMat != null
            ? palette.metalMat
            : FallbackMat(new Color(0.30f, 0.30f, 0.32f));

        var rng = new System.Random(propSeed);
        int floors = 0, placed = 0;

        foreach (var r in AllRenderers())
        {
            string n = r.gameObject.name.ToLowerInvariant();
            if (!Has(n, "zemin", "doseme", "döşeme", "floor")) continue;

            Bounds b = r.bounds;
            float inset = 0.6f;
            float w = b.size.x - inset * 2f, d = b.size.z - inset * 2f;
            float area = w * d;
            if (area < minFloorArea || w <= 0f || d <= 0f) continue;

            int count = Mathf.Min(maxPropsPerFloor, Mathf.CeilToInt(area / 25f * propDensity));
            for (int i = 0; i < count; i++)
            {
                float x = b.min.x + inset + (float)rng.NextDouble() * w;
                float z = b.min.z + inset + (float)rng.NextDouble() * d;
                Vector3 ground = new Vector3(x, b.max.y, z);      // zeminin üst yüzü
                MakeProp(rng.Next(0, 5), ground, (float)rng.NextDouble() * 360f, propMat, parent, rng);
                placed++;
            }
            floors++;
        }

        Debug.Log(placed == 0
            ? "[SceneDresser] 0 prop: sahnede adı Zemin/Floor/Doseme içeren (ve ≥" + minFloorArea +
              " m²) yüzey bulunamadı."
            : $"[SceneDresser] {floors} zemine toplam {placed} prop serpildi ('{PropContainer}').", this);
    }

    void MakeProp(int type, Vector3 groundPos, float yaw, Material mat, Transform parent, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        switch (type)
        {
            case 0:   // Kasa
            {
                float s = R(0.7f, 1.2f);
                Prim(PrimitiveType.Cube, "Kasa", groundPos + Vector3.up * s * 0.5f,
                     new Vector3(s, s, s), yaw, mat, parent);
                break;
            }
            case 1:   // Varil
            {
                float h = R(0.8f, 1.1f);
                Prim(PrimitiveType.Cylinder, "Varil", groundPos + Vector3.up * h * 0.5f,
                     new Vector3(0.7f, h * 0.5f, 0.7f), yaw, mat, parent);
                break;
            }
            case 2:   // Boru (yatık)
            {
                float len = R(1.5f, 3.5f);
                var go = Prim(PrimitiveType.Cylinder, "Boru", groundPos + Vector3.up * 0.22f,
                              new Vector3(0.44f, len * 0.5f, 0.44f), yaw, mat, parent);
                go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                break;
            }
            case 3:   // Moloz kümesi
            {
                int chunks = rng.Next(2, 5);
                for (int c = 0; c < chunks; c++)
                {
                    float s = R(0.18f, 0.45f);
                    Vector3 off = new Vector3(R(-0.6f, 0.6f), s * 0.5f, R(-0.6f, 0.6f));
                    Prim(PrimitiveType.Cube, "Moloz", groundPos + off,
                         new Vector3(s, s * R(0.5f, 1f), s), R(0f, 360f), mat, parent);
                }
                break;
            }
            default:  // Kablo (yerde ince silindir)
            {
                float len = R(1.5f, 4f);
                var go = Prim(PrimitiveType.Cylinder, "Kablo", groundPos + Vector3.up * 0.05f,
                              new Vector3(0.1f, len * 0.5f, 0.1f), yaw, mat, parent);
                go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                break;
            }
        }
    }

    // ───────────────────────── Yardımcılar ─────────────────────────

    // İşlenecek tüm MeshRenderer'lar (root boşsa tüm sahne)
    IEnumerable<MeshRenderer> AllRenderers()
    {
        var arr = root != null ? root.GetComponentsInChildren<MeshRenderer>(true)
                               : FindObjectsOfType<MeshRenderer>(true);
        foreach (var r in arr)
        {
            if (r == null) continue;
            // Kendi ürettiğimiz container'ların içini yeniden işleme
            if (r.transform.IsChildOf(transform)) continue;
            yield return r;
        }
    }

    // Sadece KENDİ container'ını temizleyip yeniden oluşturur — sahneye dokunmaz
    Transform FreshContainer(string name)
    {
        var old = transform.Find(name);
        if (old) DestroyImmediate(old.gameObject);
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        return t;
    }

    bool IsSkipped(string rawName)
    {
        string n = rawName.ToLowerInvariant();
        if (skipKeywords == null) return false;
        foreach (var k in skipKeywords)
            if (!string.IsNullOrEmpty(k) && n.Contains(k.ToLowerInvariant())) return true;
        return false;
    }

    static bool Has(string n, params string[] keys)
    {
        foreach (var k in keys) if (n.Contains(k)) return true;
        return false;
    }

    bool IsDefaultMaterial(Material m)
    {
        if (m == null) return true;
#if UNITY_EDITOR
        string path = UnityEditor.AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(path)) return false;   // sahneye gömülü materyal — dokunma
        if (path.Contains("unity_builtin_extra") ||
            path.Contains("unity default resources") ||
            path.Contains("HDRPDefaultResources") ||
            path.StartsWith("Packages/com.unity.render-pipelines")) return true;
#endif
        return m.name.StartsWith("Default");
    }

    GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, float yaw, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.transform.rotation   = Quaternion.Euler(0f, yaw, 0f);
        go.isStatic = true;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    Material FallbackMat(Color c)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.15f);
        return m;
    }
}
}
