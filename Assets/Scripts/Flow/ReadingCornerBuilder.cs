using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Okuma köşesi (kitaplık + koltuk + sehpa + lamba) — BİLEREK ReceptionBuilder'dan AYRI,
// bağımsız bir builder. Sebep: buradaki kitaplara/nesnelere ileride "okunabilir" (etkileşimli)
// davranış eklenecek. ReceptionBuilder'ın "Resepsiyon Kur" komutu HER ÇALIŞTIRILDIĞINDA kendi
// container'ını TAMAMEN SİLİP YENİDEN KURUYOR — okuma köşesi onun içinde olsaydı, masaya/
// koltuğa dokunan her küçük ayar buradaki elle eklenmiş etkileşim component'lerini de silerdi.
// Bu script kendi container'ını YÖNETİR, ReceptionBuilder'a hiç dokunmaz/bağımlı değildir
// (ReceptionBuilder/EntranceFacadeBuilder/AcidPoolBuilder ile aynı, self-contained desen).
//
// Kullanım: resepsiyon odasında, kitaplığın duracağı yere boş bir GameObject koy, bu
// script'i ekle, konumlandır/döndür (+Z = kitaplığın öne baktığı yön, koltuk oradadır),
// ⋮ "Okuma Köşesi Kur". Tekrar çalıştırınca SADECE kendi "OkumaKosesiProps" child'ını yeniler.
namespace Bloodrush.Flow
{
public class ReadingCornerBuilder : MonoBehaviour
{
    const string ContainerName = "OkumaKosesiProps";

    [Header("Asset Slotu (boşsa greybox)")]
    [Tooltip("Gerçek koltuk/kanepe modeli — boşsa greybox kanepe kullanılır.")]
    [SerializeField] GameObject seatingPrefab;

    [Header("Kitaplık")]
    [Tooltip("Kaç kitaplık ünitesi yan yana dizilsin.")]
    [SerializeField] int    shelfUnitCount = 3;
    [SerializeField] float  shelfUnitWidth = 1.3f;
    [SerializeField] float  shelfHeight    = 2.3f;
    [Tooltip("Her ünitede kaç yatay raf olsun.")]
    [SerializeField] int    shelfRows      = 5;
    [SerializeField] Color  shelfColor     = new Color(0.30f, 0.22f, 0.14f);   // ahşap ton
    [Tooltip("Kitap sırtları için renk paleti — her kitap rastgele bunlardan birini alır.")]
    [SerializeField] Color[] bookColors =
    {
        new Color(0.55f, 0.12f, 0.12f), new Color(0.15f, 0.35f, 0.55f), new Color(0.65f, 0.55f, 0.15f),
        new Color(0.20f, 0.45f, 0.25f), new Color(0.35f, 0.20f, 0.45f), new Color(0.60f, 0.35f, 0.15f),
    };

    [Header("Koltuk / Renkler")]
    [SerializeField] bool  buildRug   = true;
    [SerializeField] Color seatColor  = new Color(0.16f, 0.17f, 0.20f);   // koyu döşeme
    [SerializeField] Color frameColor = new Color(0.42f, 0.43f, 0.45f);   // fırçalı metal
    [SerializeField] Color rugColor   = new Color(0.22f, 0.14f, 0.14f);   // koyu bordo halı

    Material shelfMat, seatMat, frameMat, rugMat;
    Material[] bookMats;

    [ContextMenu("Okuma Köşesi Kur")]
    void Build()
    {
        var old = transform.Find(ContainerName);
        if (old) DestroyImmediate(old.gameObject);
        var root = new GameObject(ContainerName).transform;
        root.SetParent(transform, false);

        shelfMat = LitMat(shelfColor);
        seatMat  = LitMat(seatColor);
        frameMat = LitMat(frameColor);
        rugMat   = LitMat(rugColor);
        bookMats = new Material[Mathf.Max(1, bookColors.Length)];
        for (int i = 0; i < bookMats.Length; i++)
            bookMats[i] = LitMat(bookColors.Length > 0 ? bookColors[i] : Color.gray);

        // Kitaplık ünitelerini yan yana diz (duvara yaslı, root'un arkasında/local Z≈0)
        var rng = new System.Random(7);
        float totalWidth = shelfUnitCount * shelfUnitWidth;
        float startX = -totalWidth * 0.5f + shelfUnitWidth * 0.5f;
        for (int u = 0; u < shelfUnitCount; u++)
            BuildShelfUnit(root, startX + u * shelfUnitWidth, rng);

        // Koltuk + sehpa + halı, kitaplığın önünde (local +Z = odaya doğru)
        float seatZ = 1.6f;
        if (buildRug)
            MakeBox("OkumaHali", new Vector3(0f, 0.02f, seatZ + 0.3f), new Vector3(2.8f, 0.04f, 2.4f), rugMat, root, false);

        var seatGrp = new GameObject("OkumaKoltugu").transform;
        seatGrp.SetParent(root, false);
        seatGrp.localPosition = new Vector3(-0.4f, 0f, seatZ);
        seatGrp.localRotation = Quaternion.Euler(0f, 180f, 0f);   // kitaplığa sırtını dönüp odaya baksın
        if (seatingPrefab != null)
            InstancePrefab(seatingPrefab, Vector3.zero, Quaternion.identity, 1f, seatGrp);
        else
            GreyboxSofa(seatGrp);

        float sehpaX = 1.3f, sehpaZ = seatZ + 0.1f;
        MakeBox("OkumaSehpaUst", new Vector3(sehpaX, 0.4f, sehpaZ), new Vector3(0.7f, 0.06f, 0.7f), frameMat, root);
        for (int lx = -1; lx <= 1; lx += 2)
        for (int lz = -1; lz <= 1; lz += 2)
            MakeBox("OkumaSehpaAyak", new Vector3(sehpaX + lx * 0.28f, 0.19f, sehpaZ + lz * 0.28f), new Vector3(0.06f, 0.38f, 0.06f), frameMat, root);

        BuildReadingLamp(root, new Vector3(sehpaX + 0.6f, 0f, sehpaZ + 0.3f));

        Debug.Log("[ReadingCornerBuilder] Okuma köşesi kuruldu.", this);
    }

    // Bir kitaplık ünitesi: arka panel + yan çerçeve + üst + yatay raflar + raflardaki kitaplar
    void BuildShelfUnit(Transform root, float ux, System.Random rng)
    {
        var unit = new GameObject("KitaplikUnitesi").transform;
        unit.SetParent(root, false);
        unit.localPosition = new Vector3(ux, 0f, 0f);

        float w = shelfUnitWidth - 0.1f;      // dış genişlik (yan çerçeveler dahil)
        float innerW = w - 0.14f;             // kitapların sığacağı iç genişlik (çerçeve payı düşülmüş)
        float depth = 0.32f;

        MakeBox("ArkaPanel", new Vector3(0f, shelfHeight * 0.5f, -depth * 0.42f), new Vector3(w, shelfHeight, 0.03f), shelfMat, unit, false);
        MakeBox("YanSol", new Vector3(-w * 0.5f, shelfHeight * 0.5f, 0f), new Vector3(0.04f, shelfHeight, depth), shelfMat, unit, false);
        MakeBox("YanSag", new Vector3( w * 0.5f, shelfHeight * 0.5f, 0f), new Vector3(0.04f, shelfHeight, depth), shelfMat, unit, false);
        MakeBox("Ust", new Vector3(0f, shelfHeight, 0f), new Vector3(w, 0.04f, depth), shelfMat, unit, false);

        int rows = Mathf.Max(1, shelfRows);
        for (int r = 0; r <= rows; r++)
        {
            float ry = shelfHeight * r / rows;
            MakeBox($"Raf_{r}", new Vector3(0f, ry, 0f), new Vector3(w, 0.03f, depth), shelfMat, unit, false);
            if (r < rows) AddBooksOnShelf(unit, ry, innerW, depth, rng);
        }
    }

    // Bir raf üzerine, kitap sırtları gibi dizilmiş ince renkli bloklar (rastgele boy/renk,
    // ara ara boşluk — tıka basa dolu görünmesin). bookMats Build()'te bir kez hazırlanır,
    // her kitap için yeni materyal ÜRETİLMEZ (performans).
    void AddBooksOnShelf(Transform unit, float shelfY, float innerW, float depth, System.Random rng)
    {
        if (bookMats == null || bookMats.Length == 0) return;
        float x = -innerW * 0.5f;
        float limit = innerW * 0.5f;
        while (x < limit)
        {
            float bw = 0.03f + (float)rng.NextDouble() * 0.035f;
            if (rng.NextDouble() < 0.16) { x += bw + 0.05f; continue; }   // ara ara boşluk
            if (x + bw > limit) break;

            float bh = 0.16f + (float)rng.NextDouble() * 0.14f;
            float bd = depth * (0.55f + (float)rng.NextDouble() * 0.3f);
            var mat = bookMats[rng.Next(bookMats.Length)];
            MakeBox("Kitap", new Vector3(x + bw * 0.5f, shelfY + bh * 0.5f + 0.017f, 0f), new Vector3(bw, bh, bd), mat, unit, false);
            x += bw + 0.008f;
        }
    }

    // Basit okuma lambası: direk + baza + abajur (hafif emissive) + sıcak point ışık
    void BuildReadingLamp(Transform root, Vector3 pos)
    {
        MakeCyl("LambaBaza",  pos + new Vector3(0f, 0.02f, 0f), new Vector3(0.18f, 0.02f, 0.18f), frameMat, root, false);
        MakeCyl("LambaDirek", pos + new Vector3(0f, 0.75f, 0f), new Vector3(0.04f, 0.75f, 0.04f), frameMat, root, false);
        var shadeMat = UnlitMat(new Color(1f, 0.92f, 0.75f) * 1.8f);
        MakeSph("LambaAbajur", pos + new Vector3(0f, 1.55f, 0f), new Vector3(0.36f, 0.28f, 0.36f), shadeMat, root, false);

        var lgo = new GameObject("LambaIsigi");
        lgo.transform.SetParent(root, false);
        lgo.transform.localPosition = pos + new Vector3(0f, 1.5f, 0f);
        var light   = lgo.AddComponent<Light>();
        light.type  = LightType.Point;
        light.color = new Color(1f, 0.9f, 0.75f);
        light.range = 4.5f;
        var hd = lgo.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(900f, LightUnit.Lumen);
        hd.EnableShadows(false);
    }

    // Düzgün oranlı greybox kanepe (taban + minderler + kol + ayak) — ReceptionBuilder'daki
    // ile aynı desen, bu script kendi kopyasını taşır (self-contained, bağımlılık yok).
    void GreyboxSofa(Transform grp)
    {
        MakeBox("Taban",     new Vector3(0f, 0.16f, -0.05f), new Vector3(2.2f, 0.30f, 0.85f), seatMat,  grp);
        MakeBox("Minder",    new Vector3(0f, 0.40f, 0.05f),  new Vector3(2.0f, 0.16f, 0.70f), seatMat,  grp);
        var back = MakeBox("SirtMinder", new Vector3(0f, 0.66f, -0.34f), new Vector3(2.0f, 0.55f, 0.18f), seatMat, grp);
        back.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);   // hafif yatık sırt
        MakeBox("KolSol",    new Vector3(-1.05f, 0.38f, -0.05f), new Vector3(0.2f, 0.45f, 0.85f), seatMat, grp);
        MakeBox("KolSag",    new Vector3( 1.05f, 0.38f, -0.05f), new Vector3(0.2f, 0.45f, 0.85f), seatMat, grp);
        for (int lx = -1; lx <= 1; lx += 2)
        for (int lz = -1; lz <= 1; lz += 2)
            MakeBox("Ayak", new Vector3(lx * 0.95f, 0.05f, -0.05f + lz * 0.35f), new Vector3(0.1f, 0.1f, 0.1f), frameMat, grp);
    }

    // ───────── Yardımcılar ─────────
    GameObject MakeBox(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collider = true)
        => MakePrim(PrimitiveType.Cube, name, pos, size, mat, parent, collider);
    GameObject MakeCyl(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collider = true)
        => MakePrim(PrimitiveType.Cylinder, name, pos, size, mat, parent, collider);
    GameObject MakeSph(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collider = true)
        => MakePrim(PrimitiveType.Sphere, name, pos, size, mat, parent, collider);

    GameObject MakePrim(PrimitiveType t, string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collider)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        if (!collider) { var c = go.GetComponent<Collider>(); if (c) DestroyImmediate(c); }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = size;
        go.isStatic = true;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    void InstancePrefab(GameObject prefab, Vector3 localPos, Quaternion localRot, float scale, Transform parent)
    {
        var go = Instantiate(prefab, parent);
        go.name = prefab.name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale    = prefab.transform.localScale * scale;
    }

    Material LitMat(Color c)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    Material UnlitMat(Color hdr)
    {
        var m = new Material(Shader.Find("HDRP/Unlit"));
        m.SetColor("_UnlitColor", hdr);
        return m;
    }
}
}
