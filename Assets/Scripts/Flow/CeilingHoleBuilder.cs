using UnityEngine;

namespace Bloodrush.Flow
{
// Tavanda DELIK acar: oyuncunun yukaridan atildigi yer belli olsun (CH2 girisindeki dusme).
//
// Tavan tek parca bir plaka oldugu icin gercek bir delik ancak plakayi BOLEREK acilir:
// orijinal tavan gizlenir, yerine ayni materyal ve kalinlikta DORT parca kurulur, ortada
// bosluk kalir. Bosluktan yukari karanlik bir baca, kenarinda acik kalmis bir izgara kapak
// ve altinda birkac moloz parcasi.
//
// LabPropScatter/DebrisScatter deseni: bos bir objeyi delik merkezine koy, dis menusunden
// "Deligi Ac". Her sey kendi container child'i altinda kurulur; "Deligi Kapat" ile geri alinir
// (orijinal tavan silinmez, sadece gizlenir).
public class CeilingHoleBuilder : MonoBehaviour
{
    const string Container = "TavanDeligi";

    [Header("Delik")]
    [Tooltip("Bosluk olcusu (X, Z) metre.")]
    [SerializeField] Vector2 holeSize = new Vector2(1.6f, 1.6f);
    [Tooltip("ACIK: delik bu objenin degil, sahnedeki oyuncunun tam ustune acilir.")]
    [SerializeField] bool    alignToPlayer = true;
    [Tooltip("Bossa bu objenin ustundeki tavan isinla bulunur.")]
    [SerializeField] Transform ceiling;

    [Header("Baca (delikten yukarisi)")]
    [SerializeField] bool  shaft       = true;
    [SerializeField] float shaftHeight = 3f;
    [Tooltip("Baca duvarlarinin kalinligi.")]
    [SerializeField] float shaftWall   = 0.1f;

    [Header("Kapak")]
    [Tooltip("Delik kenarinda acik kalmis izgara kapak.")]
    [SerializeField] bool  hatch      = true;
    [SerializeField] float hatchAngle = 62f;
    [SerializeField] int   hatchBars  = 5;

    [Header("Altindaki moloz")]
    [SerializeField] bool  floorRubble  = true;
    [SerializeField] int   rubbleCount  = 8;
    [SerializeField] float rubbleRadius = 1.3f;
    [SerializeField] int   seed         = 909;

    [Header("Bacadaki isik")]
    [Tooltip("Delik karanlikta da okunsun diye bacanin icine soguk, zayif bir isik.")]
    [SerializeField] bool  shaftLight  = true;
    [SerializeField] float lightLumen  = 400f;
    [SerializeField] Color lightColor  = new Color(0.68f, 0.78f, 1f);

    [Header("Materyaller (bos = duz renk uretilir)")]
    [Tooltip("Bacanin ic yuzeyi. Bossa tavanin kendi materyali kullanilir.")]
    public Material shaftMaterial;
    [Tooltip("Bacanin tepesi — karanlik kapak, gokyuzu gorunmesin.")]
    public Material capMaterial;
    [Tooltip("Izgara kapak ve cubuklar.")]
    public Material hatchMaterial;
    [Tooltip("Yerdeki moloz.")]
    public Material rubbleMaterial;

    [ContextMenu("Deligi Ac")]
    void Build()
    {
        var old = transform.Find(Container);
        if (old != null) DestroyImmediate(old.gameObject);

        Physics.SyncTransforms();
        var slab = ceiling != null ? ceiling : FindCeiling();
        if (slab == null)
        {
            Debug.LogError("[CeilingHoleBuilder] Tavan bulunamadi — objeyi tavanin ALTINA koy ya da " +
                           "'Ceiling' alanina tavan plakasini surukle.", this);
            return;
        }

        var rend = slab.GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError("[CeilingHoleBuilder] Tavan objesinde Renderer yok.", this);
            return;
        }
        if (Quaternion.Angle(slab.rotation, Quaternion.identity) > 1f)
            Debug.LogWarning("[CeilingHoleBuilder] Tavan dondurulmus — delik eksenlere hizali kesilir, " +
                             "sonuc kaymis gorunebilir.", this);

        Bounds b = rend.bounds;                     // dunya uzayinda, eksen hizali
        Vector3 center = HoleCenter();
        float hx = Mathf.Max(0.2f, holeSize.x) * 0.5f;
        float hz = Mathf.Max(0.2f, holeSize.y) * 0.5f;
        float x0 = center.x - hx, x1 = center.x + hx;
        float z0 = center.z - hz, z1 = center.z + hz;

        if (x0 < b.min.x || x1 > b.max.x || z0 < b.min.z || z1 > b.max.z)
        {
            Debug.LogError("[CeilingHoleBuilder] Delik tavan plakasinin disina tasiyor — objeyi tavanin " +
                           "altina getir ya da Hole Size'i kucult.", this);
            return;
        }

        if ((transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
            Debug.LogWarning("[CeilingHoleBuilder] Bu objenin olcegi 1 degil — parcalar yanlis " +
                             "boyutta kurulur. Scale'i (1,1,1) yap.", this);

        var root = new GameObject(Container).transform;
        root.SetParent(transform, false);
        root.position = Vector3.zero;
        root.rotation = Quaternion.identity;

        var mat      = rend.sharedMaterial;
        var shaftMat = shaftMaterial != null ? shaftMaterial : mat;
        float yMid   = b.center.y;
        float thick  = b.size.y;

        // 1) Tavani dort parca halinde yeniden kur (ortada bosluk).
        Slab(root, "Tavan_Guney", new Vector3(b.center.x, yMid, (b.min.z + z0) * 0.5f),
             new Vector3(b.size.x, thick, Mathf.Max(0.001f, z0 - b.min.z)), mat);
        Slab(root, "Tavan_Kuzey", new Vector3(b.center.x, yMid, (z1 + b.max.z) * 0.5f),
             new Vector3(b.size.x, thick, Mathf.Max(0.001f, b.max.z - z1)), mat);
        Slab(root, "Tavan_Bati",  new Vector3((b.min.x + x0) * 0.5f, yMid, center.z),
             new Vector3(Mathf.Max(0.001f, x0 - b.min.x), thick, z1 - z0), mat);
        Slab(root, "Tavan_Dogu",  new Vector3((x1 + b.max.x) * 0.5f, yMid, center.z),
             new Vector3(Mathf.Max(0.001f, b.max.x - x1), thick, z1 - z0), mat);

        // 2) Baca: delikten yukari karanlik bir kuyu (ustu kapali, gokyuzu gorunmez).
        if (shaft && shaftHeight > 0.05f)
        {
            float top = b.max.y;
            float mid = top + shaftHeight * 0.5f;
            Slab(root, "Baca_Guney", new Vector3(center.x, mid, z0 - shaftWall * 0.5f),
                 new Vector3(holeSize.x + shaftWall * 2f, shaftHeight, shaftWall), shaftMat);
            Slab(root, "Baca_Kuzey", new Vector3(center.x, mid, z1 + shaftWall * 0.5f),
                 new Vector3(holeSize.x + shaftWall * 2f, shaftHeight, shaftWall), shaftMat);
            Slab(root, "Baca_Bati",  new Vector3(x0 - shaftWall * 0.5f, mid, center.z),
                 new Vector3(shaftWall, shaftHeight, holeSize.y), shaftMat);
            Slab(root, "Baca_Dogu",  new Vector3(x1 + shaftWall * 0.5f, mid, center.z),
                 new Vector3(shaftWall, shaftHeight, holeSize.y), shaftMat);
            Slab(root, "Baca_Tepe",  new Vector3(center.x, top + shaftHeight, center.z),
                 new Vector3(holeSize.x + shaftWall * 2f, shaftWall, holeSize.y + shaftWall * 2f),
                 capMaterial != null ? capMaterial : shaftMat);

            if (shaftLight)
            {
                var go = new GameObject("Baca_Isigi");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(center.x, top + shaftHeight * 0.55f, center.z);
                var l = go.AddComponent<Light>();
                l.type      = LightType.Point;
                l.color     = lightColor;
                l.range     = shaftHeight + 4f;
                l.intensity = lightLumen;
                var hd = go.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                if (hd == null) hd = go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                hd.EnableShadows(false);
                hd.SetIntensity(lightLumen, UnityEngine.Rendering.HighDefinition.LightUnit.Lumen);
            }
        }

        // 3) Acik kalmis izgara kapak: deligin guney kenarindan asagi sarkar.
        if (hatch)
        {
            var hinge = new GameObject("Kapak").transform;
            hinge.SetParent(root, false);
            hinge.position = new Vector3(center.x, b.min.y - 0.02f, z0);
            hinge.rotation = Quaternion.Euler(hatchAngle, 0f, 0f);

            var hm = hatchMaterial != null ? hatchMaterial : mat;
            float len = holeSize.y;
            Slab(hinge, "Cerceve_Bati", hinge.TransformPoint(new Vector3(-holeSize.x * 0.5f, 0f, len * 0.5f)),
                 new Vector3(0.05f, 0.05f, len), hm, hinge.rotation);
            Slab(hinge, "Cerceve_Dogu", hinge.TransformPoint(new Vector3(holeSize.x * 0.5f, 0f, len * 0.5f)),
                 new Vector3(0.05f, 0.05f, len), hm, hinge.rotation);
            for (int i = 0; i < Mathf.Max(1, hatchBars); i++)
            {
                float t = (i + 0.5f) / Mathf.Max(1, hatchBars);
                Slab(hinge, $"Cubuk_{i}", hinge.TransformPoint(new Vector3(0f, 0f, len * t)),
                     new Vector3(holeSize.x, 0.03f, 0.04f), hm, hinge.rotation);
            }
        }

        // 4) Altina dokulmus moloz — "buradan bir sey dustu".
        if (floorRubble && rubbleCount > 0)
        {
            var prev = Random.state;
            Random.InitState(seed);
            var rubbleRoot = new GameObject("Moloz").transform;
            rubbleRoot.SetParent(root, false);

            float floorY = FindFloorY(center);
            for (int i = 0; i < rubbleCount; i++)
            {
                Vector2 o = Random.insideUnitCircle * rubbleRadius;
                float s = Random.Range(0.05f, 0.18f);
                var scale = new Vector3(s * Random.Range(0.7f, 1.3f), s * Random.Range(0.4f, 0.8f),
                                        s * Random.Range(0.7f, 1.3f));
                Slab(rubbleRoot, $"Parca_{i}", new Vector3(center.x + o.x, floorY + scale.y * 0.35f, center.z + o.y),
                     scale, rubbleMaterial != null ? rubbleMaterial : mat,
                     Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f)),
                     collider: false);
            }
            Random.state = prev;
        }

        slab.gameObject.SetActive(false);   // orijinal tavan SILINMEZ, gizlenir
        Debug.Log($"[CeilingHoleBuilder] Delik acildi: merkez ({center.x:0.00}, {center.z:0.00}), " +
                  $"olcu {holeSize.x:0.0}x{holeSize.y:0.0}. Orijinal tavan '{slab.name}' gizlendi.", this);
    }

    [ContextMenu("Deligi Kapat (geri al)")]
    void Restore()
    {
        var old = transform.Find(Container);
        if (old != null) DestroyImmediate(old.gameObject);
        var slab = ceiling != null ? ceiling : FindCeiling();
        if (slab != null) slab.gameObject.SetActive(true);
        Debug.Log("[CeilingHoleBuilder] Delik kapatildi, orijinal tavan geri acildi.", this);
    }

    // ── Yardimcilar ────────────────────────────────────────────────────

    Vector3 HoleCenter()
    {
        if (alignToPlayer)
        {
            var player = FindFirstObjectByType<Bloodrush.Player.PlayerMovement>();
            if (player != null) return new Vector3(player.transform.position.x, transform.position.y,
                                                   player.transform.position.z);
            Debug.LogWarning("[CeilingHoleBuilder] Oyuncu bulunamadi — delik bu objenin ustune acildi.", this);
        }
        return transform.position;
    }

    // Gizlenmis tavani da bulabilmek icin once isin, sonra isimden arama.
    Transform FindCeiling()
    {
        Vector3 origin = HoleCenter();
        origin.y = transform.position.y;
        if (Physics.Raycast(origin, Vector3.up, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.transform;

        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Ceiling" || t.name == "Tavan")
            {
                var r = t.GetComponent<Renderer>();
                if (r != null && r.bounds.Contains(new Vector3(origin.x, r.bounds.center.y, origin.z))) return t;
            }
        return null;
    }

    float FindFloorY(Vector3 center)
    {
        Vector3 from = new Vector3(center.x, transform.position.y + 0.5f, center.z);
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return transform.position.y;
    }

    static void Slab(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat) =>
        Slab(parent, name, worldPos, size, mat, Quaternion.identity, true);

    static void Slab(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat,
                     Quaternion rot, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position   = worldPos;
        go.transform.rotation   = rot;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        if (!collider)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? transform.position : HoleCenter();
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(c.x, transform.position.y, c.z),
                            new Vector3(holeSize.x, 0.05f, holeSize.y));
    }
}
}
