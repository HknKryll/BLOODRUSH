using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Katlı dairesel server arenası (Ch3 — foto: iç içe yükselen halka katları +
// 4 kardinal merdiven + yüksek merkez çekirdek). Boş GO'ya ekle, ⋮ → "Arenayı Kur".
// Katlar merkeze doğru yükselir; her kat arası 4 kardinal yönde merdivenle bağlı.
// Oyuncu merdivenle VEYA kanca/wall-jump ile katlar arası çıkar.
public class ArenaBuilder : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] float radius         = 28f;   // dış yarıçap (zemin)
    [SerializeField] float wallHeight      = 9f;
    [SerializeField] int   perimeterSegments = 48;
    [Tooltip("4 kardinal girişin yarı açısı (derece). 0 = duvar tamamen kapalı.")]
    [SerializeField] float entranceHalfAngle = 6f;

    [Header("Tavan")]
    [SerializeField] bool  buildCeiling    = true;
    [SerializeField] float ceilingHeadroom = 3f;   // çekirdek tepesi ile tavan arası boşluk

    [Header("Tavan Panelleri (emissive floresan)")]
    [SerializeField] bool  buildPanels     = true;
    [SerializeField] int   panelRings      = 2;     // panel halka sayısı
    [SerializeField] int   panelsPerRing   = 10;
    [SerializeField] Vector2 panelSize      = new Vector2(3f, 1.5f);   // teğet × radyal
    [SerializeField] Color panelColor      = new Color(0.85f, 0.92f, 1f);  // soğuk floresan
    [SerializeField] float panelGlow       = 2.5f;  // emissive parlaklık çarpanı (Bloom eşiği üstü)
    [SerializeField] float panelLightLumen = 6000f; // her panelin altındaki gerçek ışık (0 = kapalı)

    [Header("Katlar (yükselen halkalar)")]
    [SerializeField] int   tierCount       = 3;   // zemin üstü kat sayısı
    [SerializeField] float groundRingWidth = 9f;  // dış duvar ile ilk kat arası (geniş savaş halkası)
    [SerializeField] float ringWidth       = 5f;  // üst katların radyal genişliği
    [SerializeField] float heightStep      = 3f;  // her kat bu kadar yükselir

    [Header("Merkez Çekirdek")]
    [SerializeField] float coreRadius = 5f;

    [Header("Merdiven")]
    [SerializeField] int   stepCount  = 8;
    [SerializeField] float stairWidth = 4f;

    [Header("Server Rafları (siper — zemin katı)")]
    [SerializeField] bool  buildRacks    = true;
    [SerializeField] int   racksPerRing  = 12;
    [SerializeField] Vector3 rackSize     = new Vector3(3f, 2f, 1.2f);
    [SerializeField] float aisleHalfAngle = 14f;

    [ContextMenu("Arenayı Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // Kademeli katlar: kat 0 = zemin (radius R), her kat içeri daralıp yükselir
        for (int t = 0; t <= tierCount; t++)
        {
            float rt = TierRadius(t);
            float yt = t * heightStep;
            if (rt <= coreRadius) break;
            // Kat i: tepesi yt'de olan solid disk (alttaki katı örter → kademe)
            MakeCylinder($"Kat_{t}", new Vector3(0f, yt - 0.25f, 0f), rt, Mathf.Max(0.5f, yt + 0.5f));

            // Bu kattan üst kata 4 kardinal merdiven (son kat hariç)
            float rNext = TierRadius(t + 1);
            if (t < tierCount && rNext > coreRadius)
                for (int c = 0; c < 4; c++)
                    BuildStair(c * 90f, rNext, yt, heightStep, rt - rNext);
        }

        // Merkez çekirdek (en yüksek)
        float coreY = (tierCount + 1) * heightStep;
        MakeCylinder("Cekirdek", new Vector3(0f, coreY * 0.5f, 0f), coreRadius, coreY);

        // Tavan yüksekliği: çekirdek tepesi + boşluk (oyuncu tepede doğuyor, sıkışmasın)
        float topY = Mathf.Max(wallHeight, coreY + ceilingHeadroom);

        // Dış duvar tavana kadar (4 kardinal giriş boşluğu)
        BuildWallWithGaps(topY);

        // Tavan diski — ışık dışarı sızmaz
        if (buildCeiling)
        {
            MakeCylinder("Tavan", new Vector3(0f, topY + 0.25f, 0f), radius + 0.6f, 0.5f);
            if (buildPanels) BuildCeilingPanels(topY);
        }

        // Zemin katı siper rafları (4 çapraz koridor açık)
        if (buildRacks) BuildRacks();

        // 4 spawn noktası (zemin, girişlerde)
        for (int s = 0; s < 4; s++)
        {
            float ang = s * 90f * Mathf.Deg2Rad;
            var sp = new GameObject($"SpawnPoint_{s}");
            sp.transform.SetParent(transform, false);
            sp.transform.localPosition = new Vector3(Mathf.Sin(ang) * (radius - 2f), 0.5f, Mathf.Cos(ang) * (radius - 2f));
        }

        // Oyuncu başlangıcı — merkez çekirdeğin TEPESİNDE doğar (mapin en üst noktası),
        // dışa bakar. Sahne başlayınca oyuncu buraya ışınlanır (istediğin yere taşıyabilirsin).
        var ps = new GameObject("PlayerStart");
        ps.transform.SetParent(transform, false);
        ps.transform.localPosition = new Vector3(0f, coreY + 1.2f, 0f);
        ps.transform.localRotation = Quaternion.LookRotation(Vector3.forward); // dışa bak
        ps.AddComponent<PlayerStartPoint>();
    }

    float TierRadius(int t) => t == 0 ? radius : radius - groundRingWidth - (t - 1) * ringWidth;

    // innerRadius: üst katın kenarı; baseY: alt kat yüksekliği; rise: bir kat; span: radyal mesafe
    void BuildStair(float cardinalDeg, float innerRadius, float baseY, float rise, float span)
    {
        var parent = new GameObject($"Merdiven_{cardinalDeg:0}");
        parent.transform.SetParent(transform, false);

        float ang = cardinalDeg * Mathf.Deg2Rad;
        Vector3 outDir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));   // merkezden dışa

        for (int s = 0; s < stepCount; s++)
        {
            float frac = stepCount <= 1 ? 0f : (float)s / (stepCount - 1);
            float y    = baseY + frac * rise;
            float rad  = innerRadius + span * (1f - frac);                 // alt basamak dışta, üst içte

            Vector3 pos = outDir * rad + Vector3.up * y;
            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Basamak_{s}";
            step.transform.SetParent(parent.transform, false);
            step.transform.localPosition = pos;
            step.transform.localRotation = Quaternion.LookRotation(-outDir, Vector3.up);
            step.transform.localScale    = new Vector3(stairWidth, 0.4f, span / stepCount + 0.3f);
            step.isStatic = true;
        }
    }

    void BuildWallWithGaps(float height)
    {
        var parent = new GameObject("DisDuvar");
        parent.transform.SetParent(transform, false);
        float segWidth = 2f * Mathf.PI * radius / perimeterSegments * 1.15f;

        for (int i = 0; i < perimeterSegments; i++)
        {
            float deg = (float)i / perimeterSegments * 360f;
            if (entranceHalfAngle > 0f && NearCardinal(deg, entranceHalfAngle)) continue;   // giriş boşluğu
            float ang = deg * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Sin(ang) * radius, height * 0.5f, Mathf.Cos(ang) * radius);
            MakeSegment(parent.transform, $"Duvar_{i}", pos, new Vector3(segWidth, height, 0.6f));
        }
    }

    void BuildRacks()
    {
        var parent = new GameObject("ServerRaflari");
        parent.transform.SetParent(transform, false);
        float ringRadius = radius - groundRingWidth * 0.5f;   // geniş zemin halkasının ortası

        for (int i = 0; i < racksPerRing; i++)
        {
            float deg = (float)i / racksPerRing * 360f;
            if (NearCardinal(deg, aisleHalfAngle)) continue;
            float ang = deg * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Sin(ang) * ringRadius, rackSize.y * 0.5f, Mathf.Cos(ang) * ringRadius);
            MakeSegment(parent.transform, $"Raf_{i}", pos, rackSize);
        }
    }

    // Tavanın altına ışık saçan floresan panel halkaları + panel başına gerçek ışık.
    // Tavan artık kendi kendini aydınlatıyor — simsiyah kalması imkansız.
    void BuildCeilingPanels(float topY)
    {
        var parent = new GameObject("TavanPanelleri");
        parent.transform.SetParent(transform, false);

        // Emissive görünüm: HDRP/Unlit + HDR renk (ışıktan bağımsız hep parlak, Bloom yakalar)
        var mat = new Material(Shader.Find("HDRP/Unlit"));
        mat.SetColor("_UnlitColor", panelColor * panelGlow);

        float y = topY - 0.25f;
        for (int r = 0; r < panelRings; r++)
        {
            float ringRadius = panelRings <= 1
                ? (coreRadius + radius) * 0.5f
                : Mathf.Lerp(coreRadius + 4f, radius - 4f, (float)r / (panelRings - 1));

            for (int i = 0; i < panelsPerRing; i++)
            {
                float ang = ((float)i / panelsPerRing + r * 0.5f / panelsPerRing) * 2f * Mathf.PI;
                Vector3 pos = new Vector3(Mathf.Sin(ang) * ringRadius, y, Mathf.Cos(ang) * ringRadius);

                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = $"Panel_{r}_{i}";
                panel.transform.SetParent(parent.transform, false);
                panel.transform.localPosition = pos;
                Vector3 flat = new Vector3(pos.x, 0f, pos.z);
                panel.transform.localRotation = flat.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(-flat.normalized, Vector3.up) : Quaternion.identity;
                panel.transform.localScale = new Vector3(panelSize.x, 0.15f, panelSize.y);
                DestroyImmediate(panel.GetComponent<Collider>());   // panele mermi/kanca takılmasın
                panel.GetComponent<Renderer>().sharedMaterial = mat;

                // Panelin altına gerçek ışık — tavandan odaya döküm
                if (panelLightLumen > 0f)
                {
                    var lgo = new GameObject($"PanelIsik_{r}_{i}");
                    lgo.transform.SetParent(parent.transform, false);
                    lgo.transform.localPosition = pos + Vector3.down * 0.6f;
                    var light   = lgo.AddComponent<Light>();
                    light.type  = LightType.Point;
                    light.color = panelColor;
                    light.range = topY * 1.2f;
                    var hd = lgo.AddComponent<HDAdditionalLightData>();
                    hd.SetIntensity(panelLightLumen, LightUnit.Lumen);
                    hd.EnableShadows(false);
                }
            }
        }
    }

    bool NearCardinal(float deg, float halfAngle)
    {
        for (int c = 0; c < 4; c++)
            if (Mathf.Abs(Mathf.DeltaAngle(deg, c * 90f)) <= halfAngle) return true;
        return false;
    }

    void MakeSegment(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seg.name = name;
        seg.transform.SetParent(parent, false);
        seg.transform.localPosition = pos;
        Vector3 flat = new Vector3(pos.x, 0f, pos.z);
        seg.transform.localRotation = flat.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(-flat.normalized, Vector3.up) : Quaternion.identity;
        seg.transform.localScale = size;
        seg.isStatic = true;
    }

    void MakeCylinder(string name, Vector3 localPos, float cylRadius, float cylHeight)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = new Vector3(cylRadius * 2f, cylHeight * 0.5f, cylRadius * 2f);
        go.isStatic = true;

        // Cylinder primitive'in CapsuleCollider'ı düz diske ölçeklenince dev kubbe
        // oluyor (oyuncu kayıp düşüyor) → gerçek mesh collision ile değiştir
        var cap = go.GetComponent<Collider>();
        if (cap) DestroyImmediate(cap);
        go.AddComponent<MeshCollider>();   // düz-tepeli disk collision (non-convex static)
    }
}
