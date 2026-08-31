using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Resepsiyon prop yerleştirici — EKLEMELİ (additive). Odaya AYRI bir boş obje olarak
// koyulur; ⋮ "Resepsiyon Kur" SADECE kendi "ResepsiyonProps" child'ını kurar/yeniler.
// Odanın duvar/zemin/ışık/mekaniğine DOKUNMAZ.
//
// ASSET SLOTLARI: logoTexture (PNG), plantPrefab, seatingPrefab — kendi bulduğun gerçek
// modelleri sürüklersin, onlar kullanılır; boşken geliştirilmiş greybox durur.
//
// Yön: +Z (mavi ok) = resepsiyonun DERİNLİĞİ (masa/logo bu yönde). Oyuncu -Z'den gelir.
namespace Bloodrush.Flow
{
public class ReceptionBuilder : MonoBehaviour
{
    const string ContainerName = "ResepsiyonProps";

    [Header("Asset Slotları (boşsa greybox)")]
    [Tooltip("Şirket logon (şeffaf PNG) — arkadaki ekranda backlit görünür.")]
    [SerializeField] Texture2D  logoTexture;
    [Tooltip("Gerçek bitki modeli (Asset Store/Sketchfab) — saksılara konur.")]
    [SerializeField] GameObject plantPrefab;
    [Tooltip("Gerçek koltuk/kanepe modeli — bekleme alanına konur.")]
    [SerializeField] GameObject seatingPrefab;

    [Header("Bölümler (aç/kapa)")]
    [SerializeField] bool buildLogoWall     = true;
    [SerializeField] bool buildDesk         = true;
    [SerializeField] bool buildTurnstiles   = true;
    [SerializeField] bool buildSeating      = true;
    [SerializeField] bool buildSign         = true;
    [SerializeField] bool buildPlanters     = true;
    [SerializeField] bool buildRug          = true;
    [SerializeField] bool buildCeilingAccent = true;
    [SerializeField] bool buildDeskNameplate = true;

    [Header("Ölçüler")]
    [SerializeField] float deskZ        = 5f;
    [SerializeField] float areaWidth    = 9f;
    [SerializeField] float screenWidth  = 2.6f;
    [SerializeField] float screenHeight = 1.5f;
    [SerializeField] float plantScale   = 1f;
    [Tooltip("Tavan aksan ışığının yüksekliği (odanın tavanına göre ayarla).")]
    [SerializeField] float ceilingY     = 3.5f;

    [Header("Renkler")]
    [SerializeField] Color accentColor = new Color(0.25f, 0.70f, 1.00f);   // Konsey aksan
    [SerializeField] Color deskColor   = new Color(0.34f, 0.30f, 0.27f);   // taupe/ahşap
    [SerializeField] Color seatColor   = new Color(0.16f, 0.17f, 0.20f);   // koyu döşeme
    [SerializeField] Color frameColor  = new Color(0.42f, 0.43f, 0.45f);   // fırçalı metal
    [SerializeField] Color panelColor  = new Color(0.10f, 0.11f, 0.13f);   // logo paneli
    [SerializeField] Color plantColor  = new Color(0.16f, 0.34f, 0.14f);   // bitki yeşili
    [SerializeField] Color rugColor    = new Color(0.22f, 0.14f, 0.14f);   // koyu bordo halı

    Material deskMat, seatMat, frameMat, panelMat, plantMat, rugMat, accentEmissive;

    [ContextMenu("Resepsiyon Kur")]
    void Build()
    {
        var old = transform.Find(ContainerName);
        if (old) DestroyImmediate(old.gameObject);
        var root = new GameObject(ContainerName).transform;
        root.SetParent(transform, false);

        deskMat        = LitMat(deskColor);
        seatMat        = LitMat(seatColor);
        frameMat       = LitMat(frameColor);
        panelMat       = LitMat(panelColor);
        plantMat       = LitMat(plantColor);
        rugMat         = LitMat(rugColor);
        accentEmissive = UnlitMat(accentColor * 3f);

        if (buildLogoWall)      BuildLogoWall(root);
        if (buildDesk)          BuildDesk(root);
        if (buildTurnstiles)    BuildTurnstiles(root);
        if (buildSeating)       BuildSeating(root);
        if (buildSign)          BuildSign(root);
        if (buildPlanters)      BuildPlanters(root);
        if (buildCeilingAccent) BuildCeilingAccent(root);
    }

    // ───────── Logo duvarı + ekran ─────────
    void BuildLogoWall(Transform p)
    {
        float z = deskZ + 1.4f;
        MakeBox("LogoDuvar", new Vector3(0f, 1.9f, z), new Vector3(6.5f, 3.8f, 0.3f), panelMat, p);

        float sz = z - 0.18f;
        // Bezel (çerçeve) + ekran (logo veya accent placeholder)
        MakeBox("EkranCerceve", new Vector3(0f, 1.95f, sz + 0.02f),
                new Vector3(screenWidth + 0.28f, screenHeight + 0.28f, 0.12f), frameMat, p, false);
        MakeBox("Ekran", new Vector3(0f, 1.95f, sz),
                new Vector3(screenWidth, screenHeight, 0.06f), ScreenMat(logoTexture), p, false);
    }

    // ───────── Karşılama masası ─────────
    void BuildDesk(Transform p)
    {
        float z = deskZ;
        MakeBox("Tezgah",    new Vector3(0f, 0.55f, z),        new Vector3(4f, 1.1f, 0.8f),     deskMat,  p);
        MakeBox("TezgahUst", new Vector3(0f, 1.13f, z),        new Vector3(4.4f, 0.08f, 1.05f), frameMat, p);
        MakeBox("ArkaMasa",  new Vector3(0f, 0.4f, z + 0.95f), new Vector3(3.2f, 0.8f, 0.5f),   deskMat,  p);

        if (buildDeskNameplate)   // masa önünde ışıklı isim şeridi (oyuncuya bakar)
            MakeBox("IsimSerit", new Vector3(0f, 0.72f, z - 0.41f), new Vector3(2.6f, 0.28f, 0.04f), accentEmissive, p, false);
    }

    // ───────── Turnikeler ─────────
    void BuildTurnstiles(Transform p)
    {
        float z = deskZ - 2.6f;
        for (int i = -1; i <= 1; i++)
            MakeBox($"Turnike_{i}", new Vector3(i * 1.1f, 0.5f, z), new Vector3(0.3f, 1.0f, 0.7f), frameMat, p);
        MakeBox("TurnikeSerit", new Vector3(0f, 0.95f, z), new Vector3(2.5f, 0.06f, 0.12f), accentEmissive, p, false);
    }

    // ───────── Bekleme alanı (kanepe + sehpa + halı) ─────────
    void BuildSeating(Transform p)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var grp = new GameObject(s < 0 ? "BeklemeSol" : "BeklemeSag").transform;
            grp.SetParent(p, false);
            grp.localPosition = new Vector3(s * areaWidth * 0.5f, 0f, 1.5f);
            grp.localRotation = Quaternion.Euler(0f, s < 0 ? 90f : -90f, 0f);   // merkeze bak

            if (buildRug)
                MakeBox("Hali", new Vector3(0f, 0.02f, 0.5f), new Vector3(3f, 0.04f, 2.6f), rugMat, grp, false);

            if (seatingPrefab != null)
                InstancePrefab(seatingPrefab, Vector3.zero, Quaternion.identity, 1f, grp);
            else
                GreyboxSofa(grp);

            // Sehpa (önde, ortaya doğru) — üst + 4 ayak
            MakeBox("SehpaUst", new Vector3(0f, 0.4f, 1.15f), new Vector3(1.1f, 0.06f, 0.6f), frameMat, grp);
            for (int lx = -1; lx <= 1; lx += 2)
            for (int lz = -1; lz <= 1; lz += 2)
                MakeBox("SehpaAyak", new Vector3(lx * 0.48f, 0.19f, 1.15f + lz * 0.24f), new Vector3(0.06f, 0.38f, 0.06f), frameMat, grp);
        }
    }

    // Düzgün oranlı greybox kanepe (taban + minderler + kol + ayak)
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

    // ───────── Yönlendirme tabelası ─────────
    void BuildSign(Transform p)
    {
        float x = areaWidth * 0.5f - 1f;
        float z = 0.5f;
        MakeBox("TabelaDirek", new Vector3(x, 1.0f, z), new Vector3(0.12f, 2.0f, 0.12f), frameMat, p);
        MakeBox("TabelaPano",  new Vector3(x, 1.7f, z), new Vector3(1.2f, 0.9f, 0.08f), accentEmissive, p, false);
    }

    // ───────── Saksı + bitki ─────────
    void BuildPlanters(Transform p)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * 2.6f;
            Vector3 planterPos = new Vector3(x, 0.3f, deskZ - 0.5f);
            MakeBox($"Saksi_{s}", planterPos, new Vector3(0.6f, 0.6f, 0.6f), frameMat, p);

            Vector3 top = new Vector3(x, 0.6f, deskZ - 0.5f);   // saksı üstü
            if (plantPrefab != null)
                InstancePrefab(plantPrefab, top, Quaternion.identity, plantScale, p);
            else
                GreyboxPlant(top, p);
        }
    }

    // Geliştirilmiş greybox bitki: gövde + katmanlı yaprak (kutudan iyi, placeholder)
    void GreyboxPlant(Vector3 basePos, Transform p)
    {
        float sc = plantScale;
        MakeCyl("Govde",   basePos + new Vector3(0f, 0.35f * sc, 0f), new Vector3(0.1f * sc, 0.35f * sc, 0.1f * sc), deskMat, p, false);
        MakeSph("Yaprak1", basePos + new Vector3(0f, 0.85f * sc, 0f), new Vector3(0.95f * sc, 0.7f * sc, 0.95f * sc), plantMat, p, false);
        MakeSph("Yaprak2", basePos + new Vector3(0.12f * sc, 1.15f * sc, -0.05f * sc), new Vector3(0.7f * sc, 0.6f * sc, 0.7f * sc), plantMat, p, false);
        MakeSph("Yaprak3", basePos + new Vector3(-0.1f * sc, 1.35f * sc, 0.06f * sc), new Vector3(0.55f * sc, 0.5f * sc, 0.55f * sc), plantMat, p, false);
    }

    // ───────── Tavan aksan ışığı ─────────
    void BuildCeilingAccent(Transform p)
    {
        MakeBox("TavanAksan", new Vector3(0f, ceilingY, deskZ), new Vector3(3.2f, 0.14f, 1.2f), accentEmissive, p, false);
        var lgo = new GameObject("ResepsiyonIsik");
        lgo.transform.SetParent(p, false);
        lgo.transform.localPosition = new Vector3(0f, ceilingY - 0.4f, deskZ);
        var light   = lgo.AddComponent<Light>();
        light.type  = LightType.Point;
        light.color = Color.Lerp(accentColor, Color.white, 0.5f);
        light.range = 9f;
        var hd = lgo.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(2600f, LightUnit.Lumen);
        hd.EnableShadows(false);
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

    // Ekran materyali: logo texture varsa backlit göster, yoksa accent placeholder
    Material ScreenMat(Texture2D tex)
    {
        var m = new Material(Shader.Find("HDRP/Unlit"));
        if (tex != null)
        {
            m.SetColor("_UnlitColor", Color.white * 1.6f);
            m.SetTexture("_UnlitColorMap", tex);
            m.EnableKeyword("_UNLITCOLORMAP");
        }
        else
        {
            m.SetColor("_UnlitColor", accentColor * 2.5f);
        }
        return m;
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
