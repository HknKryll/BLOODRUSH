using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Otomatik oda aydınlatması: sahne geometrisini ışın taramasıyla analiz eder,
// her yürünebilir zemin yüzeyinin (kat, halka, sahanlık) üstüne eşit aralıklı,
// tavana/duvara gömülmeyen ışıklar yerleştirir. Katlı/dairesel/dikdörtgen
// fark etmez — zemini nerede bulursa orayı aydınlatır.
// Kullanım: boş GO (konumu önemsiz) + bu script → ⋮ → "Işıkları Kur (Analiz)".
// Sadece kendi child ışıklarını yönetir; geometriye dokunmaz, tekrar çalıştırmak güvenli.
public class AutoLightRig : MonoBehaviour
{
    [Header("Analiz")]
    [Tooltip("Boşsa tüm sahnenin collider'ları taranır; dolu ise sadece bu kökün altı.")]
    [SerializeField] Transform roomRoot;
    [SerializeField] float sampleStep       = 2.5f;  // zemin tarama sıklığı
    [SerializeField] float lightSpacing     = 7f;    // iki ışık arası min mesafe
    [SerializeField] float heightAboveFloor = 3f;    // ışığın zeminden yüksekliği

    [Header("Işık")]
    [SerializeField] Color color   = new Color(1f, 0.95f, 0.85f);
    [SerializeField] float lumen   = 2200f;
    [SerializeField] float rangeMultiplier = 1.7f;
    [SerializeField] bool  shadows = false;

    [ContextMenu("Işıkları Kur (Analiz)")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Bounds b = ComputeBounds();
        if (b.size.sqrMagnitude < 1f) { Debug.LogWarning("[AutoLightRig] Geometri bulunamadı."); return; }

        var placed = new List<Vector3>();
        int count = 0;

        for (float x = b.min.x + sampleStep * 0.5f; x <= b.max.x; x += sampleStep)
        for (float z = b.min.z + sampleStep * 0.5f; z <= b.max.z; z += sampleStep)
        {
            Vector3 origin = new Vector3(x, b.max.y + 2f, z);
            var hits = Physics.RaycastAll(origin, Vector3.down, b.size.y + 4f, ~0, QueryTriggerInteraction.Ignore)
                              .OrderByDescending(h => h.point.y).ToArray();
            if (hits.Length == 0) continue;

            // Zemin: en üstteki, tavan olmayan, yukarı bakan yüzey
            float floorY = float.NaN, ceilY = b.max.y + 2f;
            foreach (var h in hits)
            {
                bool isCeiling = h.collider.name.Contains("Tavan");
                if (isCeiling) { ceilY = Mathf.Min(ceilY, h.point.y); continue; }
                if (h.normal.y < 0.5f) continue;              // duvar/eğik yüzey değil, zemin olsun
                floorY = h.point.y;
                break;
            }
            if (float.IsNaN(floorY)) continue;

            float headroom = ceilY - floorY;
            if (headroom < 1.6f) continue;                    // sıkışık boşluk, ışık koyma

            float h2 = Mathf.Min(heightAboveFloor, headroom - 0.6f);
            Vector3 pos = new Vector3(x, floorY + h2, z);

            // Aynı zeminde çok yakın ışık varsa atla (farklı kat = ayrı ışık hakkı)
            bool tooClose = placed.Any(p =>
                Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pos.x, pos.z)) < lightSpacing &&
                Mathf.Abs(p.y - pos.y) < 2f);
            if (tooClose) continue;

            if (Physics.CheckSphere(pos, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue; // geometri içinde

            MakeLight($"Isik_{count++}", pos);
            placed.Add(pos);
        }

        Debug.Log($"[AutoLightRig] {count} ışık yerleştirildi.");
    }

    [ContextMenu("Işıkları Temizle")]
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    Bounds ComputeBounds()
    {
        Collider[] cols = roomRoot != null
            ? roomRoot.GetComponentsInChildren<Collider>()
            : FindObjectsOfType<Collider>();

        Bounds b = default;
        bool first = true;
        foreach (var c in cols)
        {
            if (c.isTrigger || c is CharacterController) continue;
            if (first) { b = c.bounds; first = false; }
            else b.Encapsulate(c.bounds);
        }
        return b;
    }

    void MakeLight(string name, Vector3 worldPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.position = worldPos;

        var light   = go.AddComponent<Light>();
        light.type  = LightType.Point;
        light.color = color;
        light.range = Mathf.Max(heightAboveFloor, lightSpacing) * rangeMultiplier;

        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(lumen, LightUnit.Lumen);
        hd.EnableShadows(shadows);
    }
}
