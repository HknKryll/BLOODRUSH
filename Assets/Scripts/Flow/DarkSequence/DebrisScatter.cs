using UnityEngine;

namespace Bloodrush.Flow
{
// Yikim hissi veren enkaz: moloz yiginlari (bazilarindan filiz demiri cikar), dusmus ve
// sarkan tavan panelleri, paneli dusmus bos tavan yuvalari, kopuk kablolar, kirik borular,
// dagilmis kagitlar, cam kiriklari, is izi ve devrik tabureler.
//
// LabPropScatter deseni: bos bir objeye ekle (ör. B_EnkazYeri), dis menusunden "Enkazi Kur".
// Ayni seed = ayni dagilim; tekrar calistirinca sadece kendi child'larini yeniden kurar.
//
// Zemini ve tavani KENDISI bulur (asagi/yukari isin): isaretci yerden 10 cm yukarida dursa
// bile parcalar zemine oturur, kablolar gercek tavandan sarkar.
//
// Materyaller bilesen Inspector'dan eklenirken otomatik dolar (script .meta varsayilanlari:
// DS_Beton, DS_TavanPaneli...). Bos kalan slot icin duz renkli bir materyal uretilir.
public class DebrisScatter : MonoBehaviour
{
    [Header("Alan")]
    [Tooltip("Enkazin sacilacagi zemin alani (X, Z). Merkez = bu objenin konumu.")]
    [SerializeField] Vector2 areaSize = new Vector2(5f, 4f);
    [Tooltip("Ayni sayi = ayni dagilim. Begenmedigin dizilimi degistirmek icin degistir.")]
    [SerializeField] int     seed = 1337;
    [Tooltip("Zemini asagi isinla bul. Kapaliysa bu objenin Y'si zemin kabul edilir.")]
    [SerializeField] bool    snapToFloor = true;
    [Tooltip("Tavani yukari isinla bul. Bulunamazsa 'Ceiling Height' kullanilir.")]
    [SerializeField] bool    detectCeiling = true;
    [Tooltip("Tavanin zeminden yuksekligi (tavan bulunamazsa ya da algilama kapaliysa).")]
    [SerializeField] float   ceilingHeight = 4.5f;

    [Header("Moloz")]
    [SerializeField] int   rubblePiles   = 2;
    [SerializeField] int   chunksPerPile = 16;
    [Tooltip("Yiginlarin disina serpilen kucuk parcalar.")]
    [SerializeField] int   looseChunks   = 20;
    [SerializeField] float pileRadius    = 0.9f;
    [SerializeField] float pileHeight    = 0.45f;
    [Tooltip("Buyuk parcalarin kacindan filiz demiri ciksin.")]
    [Range(0f, 1f)]
    [SerializeField] float rebarChance   = 0.35f;

    [Header("Tavan")]
    [Tooltip("Paneli dusmus, karanlik kalmis tavan yuvalari. Her birinin altina bir panel duser.")]
    [SerializeField] int   ceilingHoles   = 2;
    [Tooltip("Tek kenarindan tavana asili kalmis panel.")]
    [SerializeField] int   danglingPanels = 1;
    [Tooltip("Rastgele yere dusmus ek paneller (bazilari yiginlara yaslanir, bazilari kirik).")]
    [SerializeField] int   fallenPanels   = 3;
    [SerializeField] int   hangingCables  = 5;
    [SerializeField] float panelSize      = 0.6f;

    [Header("Dagilmis esya")]
    [SerializeField] int brokenPipes   = 2;
    [SerializeField] int papers        = 14;
    [SerializeField] int glassShards   = 18;
    [SerializeField] int scorchMarks   = 1;
    [SerializeField] int toppledStools = 1;

    [Header("Carpisma")]
    [Tooltip("En uzun kenari bundan buyuk moloz parcalari collider alir (oyuncu takilir). " +
             "Kagit, kablo, cam, kucuk parcalar dekor — collider'siz.")]
    [SerializeField] float colliderMinSize = 0.35f;

    [Header("Materyaller (bos = duz renk uretilir)")]
    public Material concreteMat;
    public Material ceilingPanelMat;
    public Material metalMat;
    public Material cableMat;
    public Material copperMat;
    public Material paperMat;
    public Material glassMat;
    public Material sootMat;

    float floorY;   // zeminin yerel Y'si
    float ceilY;    // tavanin yerel Y'si

    [ContextMenu("Enkazi Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        EnsureMaterials();
        Physics.SyncTransforms();
        floorY = snapToFloor && ProbeLocalY(Vector3.down, out float f) ? f : 0f;
        ceilY  = detectCeiling && ProbeLocalY(Vector3.up, out float c) && c > floorY + 1f
                 ? c : floorY + ceilingHeight;

        var prevState = Random.state;
        Random.InitState(seed);

        var piles = new Vector3[Mathf.Max(0, rubblePiles)];
        for (int p = 0; p < piles.Length; p++) piles[p] = BuildPile(p);

        for (int i = 0; i < looseChunks; i++)
            Chunk($"Moloz_{i}", RandomOnFloor(1f), Random.Range(0.04f, 0.16f), transform);

        // Tavan yuvalari: her birinin altina (biraz kaymis) bir panel duser — goz bosluktan
        // yere bakinca "buradan dustu" baglantisini kurar.
        int cableIndex = 0;
        for (int h = 0; h < ceilingHoles + danglingPanels; h++)
        {
            Vector3 hole = RandomOnFloor(0.8f);
            float   yaw  = Random.Range(0f, 360f);
            CeilingHole(h, hole, yaw);

            if (h < ceilingHoles)
                FallenPanel($"DusmusPanel_Yuva{h}", hole + Flat(Random.insideUnitCircle * 0.4f),
                            Random.Range(0f, 360f), false, piles);
            else
                DanglingPanel(h, hole, yaw);

            if (cableIndex < hangingCables)
                Cable(cableIndex++, new Vector3(hole.x, ceilY, hole.z) + Flat(Random.insideUnitCircle * 0.2f));
        }
        for (; cableIndex < hangingCables; cableIndex++)
        {
            Vector3 p = RandomOnFloor(0.9f);
            Cable(cableIndex, new Vector3(p.x, ceilY, p.z));
        }

        for (int i = 0; i < fallenPanels; i++)
            FallenPanel($"DusmusPanel_{i}", RandomOnFloor(0.9f), Random.Range(0f, 360f),
                        piles.Length > 0 && Random.value < 0.5f, piles);

        for (int i = 0; i < brokenPipes;   i++) Pipe(i);
        for (int i = 0; i < scorchMarks;   i++) Scorch(i);
        for (int i = 0; i < papers;        i++) Paper(i);
        if (glassShards > 0) GlassCluster();
        for (int i = 0; i < toppledStools; i++) Stool(i);

        Random.state = prevState;
        Debug.Log($"[DebrisScatter] Enkaz kuruldu (seed {seed}) — zemin y {floorY:0.00}, " +
                  $"tavan {ceilY - floorY:0.00} m yukarida.", this);
    }

    // ── Moloz ──────────────────────────────────────────────────────────

    Vector3 BuildPile(int index)
    {
        Vector3 center = RandomOnFloor(0.65f);
        var pile = Group($"Yigin_{index}", center);

        for (int i = 0; i < chunksPerPile; i++)
        {
            float d01  = Mathf.Sqrt(Random.value);          // 0 = merkez, 1 = kenar
            float ang  = Random.value * Mathf.PI * 2f;
            float size = Mathf.Lerp(0.5f, 0.12f, d01) * Random.Range(0.7f, 1.2f);
            float y    = (1f - d01) * pileHeight * Random.Range(0.35f, 1f);
            var pos = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (d01 * pileRadius) + Vector3.up * y;
            Chunk($"Parca_{i}", pos, size, pile);
        }
        return center;
    }

    void Chunk(string name, Vector3 localPos, float size, Transform parent)
    {
        var scale = new Vector3(size * Random.Range(0.7f, 1.3f),
                                size * Random.Range(0.35f, 0.8f),
                                size * Random.Range(0.7f, 1.3f));
        var rot = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f));
        localPos.y += scale.y * 0.35f;   // hafif gomulu otursun, havada durmasin

        bool big = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) >= colliderMinSize;
        Prim(name, PrimitiveType.Cube, parent, localPos, scale, rot, concreteMat, big);

        if (size >= 0.25f && Random.value < rebarChance)
        {
            int n = Random.Range(1, 3);
            for (int i = 0; i < n; i++)
            {
                float   len = Random.Range(0.3f, 0.7f);
                Vector3 dir = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
                Prim("FilizDemiri", PrimitiveType.Cylinder, parent, localPos + dir * (len * 0.45f),
                     new Vector3(0.018f, len * 0.5f, 0.018f), Quaternion.FromToRotation(Vector3.up, dir),
                     metalMat, false);
            }
        }
    }

    // ── Tavan ──────────────────────────────────────────────────────────

    void CeilingHole(int index, Vector3 floorPos, float yaw)
    {
        // Panelin eksik oldugu yer: tavanin hemen altinda ince, kapkara bir yuva.
        Prim($"TavanYuvasi_{index}", PrimitiveType.Cube, transform,
             new Vector3(floorPos.x, ceilY - 0.006f, floorPos.z),
             new Vector3(panelSize * 0.98f, 0.01f, panelSize * 0.98f),
             Quaternion.Euler(0f, yaw, 0f), sootMat, false);
    }

    void DanglingPanel(int index, Vector3 floorPos, float yaw)
    {
        // Mentese = yuvanin bir kenari; panel ondan asagi sarkar.
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        var hinge = Group($"SarkanPanel_{index}",
                          new Vector3(floorPos.x, ceilY - 0.02f, floorPos.z) + yawRot * new Vector3(0f, 0f, -panelSize * 0.5f));
        hinge.localRotation = yawRot * Quaternion.Euler(Random.Range(55f, 80f), 0f, Random.Range(-10f, 10f));

        Prim("Panel", PrimitiveType.Cube, hinge, new Vector3(0f, 0f, panelSize * 0.5f),
             new Vector3(panelSize, 0.025f, panelSize), Quaternion.identity, ceilingPanelMat, false);
    }

    void FallenPanel(string name, Vector3 pos, float yaw, bool leanOnPile, Vector3[] piles)
    {
        bool  broken = Random.value < 0.4f;
        float sx = panelSize * (broken ? Random.Range(0.45f, 0.6f) : 1f);
        float sz = panelSize * (broken ? Random.Range(0.5f, 1f)    : 1f);
        const float th = 0.025f;
        Quaternion rot;

        if (leanOnPile && piles.Length > 0)
        {
            // Yigina yaslanan panel: kalkik kenar (-Z) yiginin uzerine gelir.
            Vector3 pile = piles[Random.Range(0, piles.Length)];
            Vector2 dir2 = Random.insideUnitCircle.normalized;
            if (dir2 == Vector2.zero) dir2 = Vector2.right;
            Vector3 dir = Flat(dir2);
            pos  = pile + dir * (pileRadius * 0.9f);
            yaw  = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float tilt = Random.Range(18f, 40f);
            rot   = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            pos.y = floorY + Mathf.Sin(tilt * Mathf.Deg2Rad) * sz * 0.5f + th * 0.5f;
        }
        else
        {
            rot   = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            pos.y = floorY + th * 0.5f + Random.Range(0.002f, 0.01f);
        }

        Prim(name, PrimitiveType.Cube, transform, pos, new Vector3(sx, th, sz), rot, ceilingPanelMat, false);
    }

    void Cable(int index, Vector3 top)
    {
        var cable = Group($"KopukKablo_{index}", top);
        float maxLen = Mathf.Max(0.5f, (ceilY - floorY) * 0.55f);
        float total  = Random.Range(0.8f, 2.2f);
        total = Mathf.Min(total, maxLen);

        int     segments = Random.Range(2, 4);
        Vector3 p   = Vector3.zero;
        Vector3 dir = Vector3.down;
        for (int s = 0; s < segments; s++)
        {
            float len = total / segments;
            Vector2 wobble = Random.insideUnitCircle * 0.3f;
            dir = (dir + new Vector3(wobble.x, 0f, wobble.y) * 0.6f).normalized;
            if (dir.y > -0.6f) dir = new Vector3(dir.x, -0.6f, dir.z).normalized;   // yukari kivrilmasin

            Prim($"Parca_{s}", PrimitiveType.Cylinder, cable, p + dir * (len * 0.5f),
                 new Vector3(0.025f, len * 0.5f, 0.025f), Quaternion.FromToRotation(Vector3.up, dir),
                 cableMat, false);
            p += dir * len;
        }

        // Kopuk ucta acikta kalmis bakir tel.
        Prim("BakirUc", PrimitiveType.Cylinder, cable, p + dir * 0.02f,
             new Vector3(0.012f, 0.025f, 0.012f), Quaternion.FromToRotation(Vector3.up, dir),
             copperMat, false);
    }

    // ── Dagilmis esya ──────────────────────────────────────────────────

    void Pipe(int index)
    {
        Vector3 start = RandomOnFloor(0.8f);
        float   yaw   = Random.Range(0f, 360f);
        var pipe = Group($"KirikBoru_{index}", start + Vector3.up * 0.05f);
        pipe.localRotation = Quaternion.Euler(0f, yaw, 0f);

        float len = Random.Range(0.7f, 1.5f);
        var along = Quaternion.Euler(90f, 0f, 0f);   // silindir ekseni +Z
        Prim("Govde", PrimitiveType.Cylinder, pipe, new Vector3(0f, 0f, len * 0.5f),
             new Vector3(0.1f, len * 0.5f, 0.1f), along, metalMat, false);
        Prim("Flans", PrimitiveType.Cylinder, pipe, new Vector3(0f, 0f, 0.02f),
             new Vector3(0.17f, 0.015f, 0.17f), along, metalMat, false);

        // Kirilip yana savrulmus kisa parca.
        float   len2 = Random.Range(0.25f, 0.5f);
        float   turn = Random.Range(25f, 50f) * (Random.value < 0.5f ? -1f : 1f);
        Vector3 end  = new Vector3(0f, 0f, len + 0.12f);
        var     rot2 = Quaternion.Euler(0f, turn, 0f);
        Prim("KirikParca", PrimitiveType.Cylinder, pipe, end + rot2 * new Vector3(0f, 0f, len2 * 0.5f),
             new Vector3(0.1f, len2 * 0.5f, 0.1f), rot2 * along, metalMat, false);
    }

    void Scorch(int index)
    {
        // Duzensiz kenarli is lekesi: bir buyuk disk + kenarina tasan kucuk diskler.
        Vector3 c   = RandomOnFloor(0.7f);
        float   dia = Random.Range(1.4f, 2.4f);
        var group = Group($"IsIzi_{index}", c);
        Prim("Merkez", PrimitiveType.Cylinder, group, new Vector3(0f, 0.004f, 0f),
             new Vector3(dia, 0.002f, dia), Quaternion.identity, sootMat, false);

        int blobs = Random.Range(3, 6);
        for (int i = 0; i < blobs; i++)
        {
            Vector2 o = Random.insideUnitCircle.normalized * dia * Random.Range(0.35f, 0.5f);
            float   d = dia * Random.Range(0.25f, 0.45f);
            Prim($"Kenar_{i}", PrimitiveType.Cylinder, group, new Vector3(o.x, 0.0045f + i * 0.0003f, o.y),
                 new Vector3(d, 0.002f, d), Quaternion.identity, sootMat, false);
        }
    }

    void Paper(int index)
    {
        Vector3 pos = RandomOnFloor(1f);
        pos.y = floorY + 0.006f + index * 0.0012f;   // ust uste binenler titremesin
        Prim($"Kagit_{index}", PrimitiveType.Cube, transform, pos, new Vector3(0.21f, 0.002f, 0.297f),
             Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f)),
             paperMat, false);
    }

    void GlassCluster()
    {
        Vector3 center = RandomOnFloor(0.8f);
        var group = Group("CamKiriklari", center);
        for (int i = 0; i < glassShards; i++)
        {
            Vector2 o = Random.insideUnitCircle * 0.6f;
            Prim($"Kirik_{i}", PrimitiveType.Cube, group, new Vector3(o.x, 0.006f, o.y),
                 new Vector3(Random.Range(0.02f, 0.07f), 0.004f, Random.Range(0.015f, 0.05f)),
                 Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)),
                 glassMat, false);
        }
    }

    void Stool(int index)
    {
        // Laboratuvar taburesi, yan yatmis. Yerel +Y = taburenin dik ekseni.
        Vector3 pos = RandomOnFloor(0.85f) + Vector3.up * 0.19f;
        var stool = Group($"DevrikTabure_{index}", pos);
        stool.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) *
                              Quaternion.Euler(0f, 0f, 90f + Random.Range(-8f, 8f));

        Prim("Oturak",  PrimitiveType.Cylinder, stool, new Vector3(0f, 0.28f, 0f),  new Vector3(0.36f, 0.025f, 0.36f), Quaternion.identity, cableMat, false);
        Prim("Direk",   PrimitiveType.Cylinder, stool, Vector3.zero,                new Vector3(0.045f, 0.27f, 0.045f), Quaternion.identity, metalMat, false);
        Prim("Ayaklik", PrimitiveType.Cylinder, stool, new Vector3(0f, -0.08f, 0f), new Vector3(0.3f, 0.008f, 0.3f),   Quaternion.identity, metalMat, false);
        Prim("Taban",   PrimitiveType.Cylinder, stool, new Vector3(0f, -0.28f, 0f), new Vector3(0.4f, 0.015f, 0.4f),   Quaternion.identity, metalMat, false);

        if (colliderMinSize <= 0.6f)
        {
            var box = stool.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.38f, 0.6f, 0.38f);
        }
    }

    // ── Yardimcilar ────────────────────────────────────────────────────

    Vector3 RandomOnFloor(float fill) =>
        new Vector3(Random.Range(-0.5f, 0.5f) * areaSize.x * fill, floorY,
                    Random.Range(-0.5f, 0.5f) * areaSize.y * fill);

    static Vector3 Flat(Vector2 v) => new Vector3(v.x, 0f, v.y);

    Transform Group(string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.localPosition = localPos;
        return t;
    }

    void Prim(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
              Quaternion rot, Material mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }
    }

    // Bu objenin konumundan dikey isin; ilk kati (trigger olmayan) yuzeyin YEREL Y'si.
    bool ProbeLocalY(Vector3 worldDir, out float localY)
    {
        localY = 0f;
        Vector3 origin = transform.position + (worldDir.y < 0f ? Vector3.up * 0.5f : Vector3.up * 0.3f);
        var hits = Physics.RaycastAll(origin, worldDir, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool  any  = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; localY = transform.InverseTransformPoint(h.point).y; any = true; }
        }
        return any;
    }

    void EnsureMaterials()
    {
        if (concreteMat     == null) concreteMat     = Lit(new Color(0.42f, 0.41f, 0.39f), 0f,   0.15f);
        if (ceilingPanelMat == null) ceilingPanelMat = Lit(new Color(0.72f, 0.71f, 0.67f), 0f,   0.2f);
        if (metalMat        == null) metalMat        = Lit(new Color(0.30f, 0.31f, 0.33f), 0.8f, 0.35f);
        if (cableMat        == null) cableMat        = Lit(new Color(0.04f, 0.04f, 0.045f), 0f,  0.4f);
        if (copperMat       == null) copperMat       = Lit(new Color(0.85f, 0.45f, 0.25f), 1f,   0.5f);
        if (paperMat        == null) paperMat        = Lit(new Color(0.78f, 0.75f, 0.68f), 0f,   0.1f);
        if (glassMat        == null) glassMat        = Lit(new Color(0.6f, 0.7f, 0.72f),   0f,   0.95f);
        if (sootMat         == null) sootMat         = Lit(new Color(0.02f, 0.018f, 0.016f), 0f, 0f);
    }

    static Material Lit(Color c, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = new Color(1f, 0.55f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, floorY, 0f), new Vector3(areaSize.x, 0.02f, areaSize.y));
    }
}
}
