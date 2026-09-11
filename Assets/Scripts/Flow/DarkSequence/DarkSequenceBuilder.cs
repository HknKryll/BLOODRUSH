using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// Karanlık Sekans'ın TÜM greybox yerleşimini tek tıkla kurar (RoomBuilder/GravityRoomBuilder
// deseni). Boş bir GameObject'e ekle, sonra sağ üstteki dişli menüsünden "Karanlık Sekansı Kur".
//
// Ne kurar: 10 alanlık oda+koridor zinciri, kapı boşluklu duvarlar, tavan ışıkları ve HER
// component için isimlendirilmiş BOŞ MARKER objeleri — script'leri o marker'lara ekleyeceksin.
// Component'lerin KENDİSİNİ kurmaz: ayarlar ve event bağlantıları senin işin.
//
// UYARI: "Kur" her çalıştığında bu objenin TÜM child'ları silinip yeniden kurulur — marker'lara
// eklediğin script'ler de gider. Yerleşimi kesinleştirdikten sonra bir daha çalıştırma
// (RoomBuilder'ın da bilinen davranışı).
public class DarkSequenceBuilder : MonoBehaviour
{
    [Header("Genel")]
    [SerializeField] float roomHeight    = 4.5f;
    [SerializeField] float wallThickness = 0.4f;
    [SerializeField] float doorWidth     = 3f;
    [SerializeField] float doorHeight    = 3f;

    [Header("Alan olculeri (uzunluk X, genislik Z)")]
    [SerializeField] Vector2 wakeRoom    = new Vector2(12f, 10f);   // A  uyanis
    [SerializeField] Vector2 exploreArea = new Vector2(22f, 12f);   // B  kesif
    [SerializeField] Vector2 sideRoom    = new Vector2(8f,  8f);    // B2 gizli sigorta odasi
    [SerializeField] Vector2 threshold   = new Vector2(5f,  6f);    // C  kilitli kapi esigi
    [SerializeField] Vector2 metalHall   = new Vector2(16f, 5f);    // D  metalik koridor
    [SerializeField] Vector2 valveRoomA  = new Vector2(10f, 10f);   // E
    [Tooltip("Iki valf arasi koridor. 14 sn'lik pencerede kosarak yetisilebilmeli — " +
             "18-20 m iyi bir baslangic.")]
    [SerializeField] Vector2 valveHall   = new Vector2(19f, 4f);    // F
    [SerializeField] Vector2 valveRoomB  = new Vector2(10f, 10f);   // G
    [SerializeField] Vector2 chaseHall   = new Vector2(30f, 5f);    // H  kacis koridoru
    [SerializeField] Vector2 finalRoom   = new Vector2(12f, 10f);   // I  son oda

    [Header("Palet")]
    [Tooltip("A-C arasi: eski/beton, karanlik bolum.")]
    [SerializeField] Color darkColor  = new Color(0.16f, 0.155f, 0.15f);
    [Tooltip("D-I arasi: metalik/endustriyel — ADIM 6'daki doku kopusu.")]
    [SerializeField] Color metalColor = new Color(0.22f, 0.235f, 0.26f);

    Material darkMat, metalMat, panelMat;
    float    cursorX;   // alanlar +X yonunde zincirleniyor

    [ContextMenu("Karanlık Sekansı Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        darkMat  = Lit(darkColor,  0.15f, 0.55f);
        metalMat = Lit(metalColor, 0.75f, 0.35f);
        panelMat = new Material(Shader.Find("HDRP/Unlit"));
        panelMat.SetColor("_UnlitColor", new Color(0.7f, 0.8f, 1f) * 2f);

        cursorX = 0f;

        // ── A: Uyanis odasi ─────────────────────────────────────────────
        var a = Shell("A_UyanisOdasi", wakeRoom, darkMat, gapEast: true);
        Marker(a, "A_PlayerStartYeri",  new Vector3(-wakeRoom.x * 0.35f, 0.1f, 0f), Vector3.right);
        Marker(a, "A_AcilIsikYeri",     new Vector3(0f, roomHeight - 0.5f, 0f));
        Marker(a, "A_WakeUpEffectYeri", new Vector3(0f, 1.5f, 0f));

        // ── B: Kesif alani (ana cikis enkazla kapali, yan odaya gecit var) ──
        var b = Shell("B_KesifAlani", exploreArea, darkMat, gapWest: true, gapEast: true, gapNorth: true);
        Marker(b, "B_EnkazYeri",      new Vector3(exploreArea.x * 0.30f, 0.1f, -exploreArea.y * 0.3f));
        Marker(b, "B_Sigorta1Yeri",   new Vector3(-exploreArea.x * 0.2f, 1.0f, exploreArea.y * 0.25f));
        Marker(b, "B_AmbiyansYeri",   new Vector3(0f, 2f, 0f));
        Marker(b, "B_SiluetNoktasi1", new Vector3(exploreArea.x * 0.45f, 0.1f, 0f), Vector3.left);
        CeilingLights(b, exploreArea, 2);

        // B2: yan oda — gizli ikinci sigorta (B'nin kuzeyinde)
        var b2 = SideShell("B2_YanOda", b, exploreArea, sideRoom, darkMat);
        Marker(b2, "B2_Sigorta2Yeri", new Vector3(0f, 1.0f, sideRoom.y * 0.3f));
        Marker(b2, "B2_EnkazYeri",    new Vector3(0f, 0.1f, 0f));

        // ── C: Kilitli kapi esigi ───────────────────────────────────────
        var c = Shell("C_KilitliKapiEsigi", threshold, darkMat, gapWest: true, gapEast: true);
        Marker(c, "C_KapiYeri",  new Vector3(threshold.x * 0.5f, 0f, 0f), Vector3.right);
        Marker(c, "C_PanelYeri", new Vector3(0f, 1.5f, threshold.y * 0.5f - wallThickness), Vector3.back);

        // ── D: Metalik koridor (palet degisimi — ADIM 6) ─────────────────
        var d = Shell("D_MetalikKoridor", metalHall, metalMat, gapWest: true, gapEast: true);
        CeilingLights(d, metalHall, 2);

        // ── E: Valf odasi A ─────────────────────────────────────────────
        var e = Shell("E_ValfOdasiA", valveRoomA, metalMat, gapWest: true, gapEast: true);
        Marker(e, "E_ValfAYeri",      new Vector3(0f, 1.3f, valveRoomA.y * 0.5f - wallThickness), Vector3.back);
        Marker(e, "E_SiluetNoktasi2", new Vector3(-valveRoomA.x * 0.5f, 0.1f, 0f), Vector3.right);
        CeilingLights(e, valveRoomA, 2);

        // ── F: Baglanti koridoru (valf kosusu) ──────────────────────────
        var f = Shell("F_BaglantiKoridoru", valveHall, metalMat, gapWest: true, gapEast: true);
        CeilingLights(f, valveHall, 2);

        // ── G: Valf odasi B + final kapi ────────────────────────────────
        var g = Shell("G_ValfOdasiB", valveRoomB, metalMat, gapWest: true, gapEast: true);
        Marker(g, "G_ValfBYeri",       new Vector3(0f, 1.3f, valveRoomB.y * 0.5f - wallThickness), Vector3.back);
        Marker(g, "G_FinalKapiYeri",   new Vector3(valveRoomB.x * 0.5f, 0f, 0f), Vector3.right);
        Marker(g, "G_ValfSekansiYeri", new Vector3(0f, 1.5f, 0f));
        CeilingLights(g, valveRoomB, 2);

        // ── H: Kacis koridoru ───────────────────────────────────────────
        var h = Shell("H_KacisKoridoru", chaseHall, metalMat, gapWest: true, gapEast: true);
        Marker(h, "H_PatlamaYeri",      new Vector3(-chaseHall.x * 0.45f, 1.5f, 0f), Vector3.right);
        Marker(h, "H_GucGeriDonusYeri", new Vector3(chaseHall.x * 0.42f, 1.5f, 0f));
        // Ilk yari: patlamayla sonecek isiklar. Ikinci yari: guc geri gelince yananlar.
        CeilingLights(h, chaseHall, 3, "SonecekIsik", -0.45f, -0.05f);
        CeilingLights(h, chaseHall, 3, "GucIsigi",     0.10f,  0.45f);

        // ── I: Son oda + cikis ──────────────────────────────────────────
        var iRoom = Shell("I_SonOda", finalRoom, metalMat, gapWest: true, gapEast: true);
        Marker(iRoom, "I_RafYeri",         new Vector3(finalRoom.x * 0.28f, 0.6f, 0f), Vector3.right);
        Marker(iRoom, "I_CikisKapiYeri",   new Vector3(finalRoom.x * 0.5f, 0f, 0f), Vector3.right);
        Marker(iRoom, "I_CikisTetigiYeri", new Vector3(finalRoom.x * 0.5f + 1.2f, 1f, 0f), Vector3.right);
        Marker(iRoom, "I_SogukIsikYeri",   new Vector3(finalRoom.x * 0.5f + 3f, 2.5f, 0f));
        CeilingLights(iRoom, finalRoom, 2);

        Debug.Log($"[DarkSequenceBuilder] Kuruldu — toplam uzunluk yaklasik {cursorX:0} m. " +
                  "Marker'lara script'leri ekle; 'Kur'u BIR DAHA calistirma (child'lar silinir).", this);
    }

    // ── Kabuk kurucular ────────────────────────────────────────────────

    // Bir alani +X zincirine ekler: zemin, tavan, 4 duvar (istenenlerde kapi boslugu).
    Transform Shell(string name, Vector2 size, Material mat,
                    bool gapWest = false, bool gapEast = false,
                    bool gapNorth = false, bool gapSouth = false)
    {
        float len = size.x, wid = size.y;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(cursorX + len * 0.5f, 0f, 0f);
        cursorX += len;

        var t = go.transform;
        Slab(t, "Zemin", new Vector3(0f, -wallThickness * 0.5f, 0f),
             new Vector3(len, wallThickness, wid), mat);
        Slab(t, "Tavan", new Vector3(0f, roomHeight + wallThickness * 0.5f, 0f),
             new Vector3(len, wallThickness, wid), mat);
        WallX(t, "Duvar_Bati",  -len * 0.5f, wid, gapWest,  mat);
        WallX(t, "Duvar_Dogu",   len * 0.5f, wid, gapEast,  mat);
        WallZ(t, "Duvar_Kuzey",  wid * 0.5f, len, gapNorth, mat);
        WallZ(t, "Duvar_Guney", -wid * 0.5f, len, gapSouth, mat);
        return t;
    }

    // Ana zincirin YANINA (kuzeyine) bir oda kurar — zinciri ilerletmez.
    Transform SideShell(string name, Transform host, Vector2 hostSize, Vector2 size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = host.localPosition +
            new Vector3(0f, 0f, hostSize.y * 0.5f + size.y * 0.5f);

        var t = go.transform;
        Slab(t, "Zemin", new Vector3(0f, -wallThickness * 0.5f, 0f),
             new Vector3(size.x, wallThickness, size.y), mat);
        Slab(t, "Tavan", new Vector3(0f, roomHeight + wallThickness * 0.5f, 0f),
             new Vector3(size.x, wallThickness, size.y), mat);
        WallX(t, "Duvar_Bati",  -size.x * 0.5f, size.y, false, mat);
        WallX(t, "Duvar_Dogu",   size.x * 0.5f, size.y, false, mat);
        WallZ(t, "Duvar_Kuzey",  size.y * 0.5f, size.x, false, mat);
        WallZ(t, "Duvar_Guney", -size.y * 0.5f, size.x, true,  mat);   // ana odaya gecit
        return t;
    }

    // Kalinligi X'te olan duvar (bati/dogu). Bosluk Z boyunca ortalanir.
    void WallX(Transform p, string name, float x, float span, bool gap, Material mat)
    {
        if (!gap)
        {
            Slab(p, name, new Vector3(x, roomHeight * 0.5f, 0f),
                 new Vector3(wallThickness, roomHeight, span), mat);
            return;
        }
        float side = Mathf.Max(0.01f, (span - doorWidth) * 0.5f);
        Slab(p, name + "_1", new Vector3(x, roomHeight * 0.5f,  (doorWidth + side) * 0.5f),
             new Vector3(wallThickness, roomHeight, side), mat);
        Slab(p, name + "_2", new Vector3(x, roomHeight * 0.5f, -(doorWidth + side) * 0.5f),
             new Vector3(wallThickness, roomHeight, side), mat);
        float lintel = roomHeight - doorHeight;
        if (lintel > 0.05f)
            Slab(p, name + "_Lento", new Vector3(x, doorHeight + lintel * 0.5f, 0f),
                 new Vector3(wallThickness, lintel, doorWidth), mat);
    }

    // Kalinligi Z'de olan duvar (kuzey/guney). Bosluk X boyunca ortalanir.
    void WallZ(Transform p, string name, float z, float span, bool gap, Material mat)
    {
        if (!gap)
        {
            Slab(p, name, new Vector3(0f, roomHeight * 0.5f, z),
                 new Vector3(span, roomHeight, wallThickness), mat);
            return;
        }
        float side = Mathf.Max(0.01f, (span - doorWidth) * 0.5f);
        Slab(p, name + "_1", new Vector3( (doorWidth + side) * 0.5f, roomHeight * 0.5f, z),
             new Vector3(side, roomHeight, wallThickness), mat);
        Slab(p, name + "_2", new Vector3(-(doorWidth + side) * 0.5f, roomHeight * 0.5f, z),
             new Vector3(side, roomHeight, wallThickness), mat);
        float lintel = roomHeight - doorHeight;
        if (lintel > 0.05f)
            Slab(p, name + "_Lento", new Vector3(0f, doorHeight + lintel * 0.5f, z),
                 new Vector3(doorWidth, lintel, wallThickness), mat);
    }

    void Slab(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    // Tavan paneli + Point Light. Isiklar KAPALI kurulur — karanlik sekans.
    void CeilingLights(Transform parent, Vector2 size, int count,
                       string namePrefix = "Isik", float xFrom = -0.35f, float xTo = 0.35f)
    {
        var holder = new GameObject(namePrefix + "lar");
        holder.transform.SetParent(parent, false);

        for (int i = 0; i < count; i++)
        {
            float f = count == 1 ? 0.5f : i / (float)(count - 1);
            float x = Mathf.Lerp(size.x * xFrom, size.x * xTo, f);
            Vector3 pos = new Vector3(x, roomHeight - 0.3f, 0f);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = $"{namePrefix}Panel_{i}";
            panel.transform.SetParent(holder.transform, false);
            panel.transform.localPosition = pos;
            panel.transform.localScale    = new Vector3(1.6f, 0.12f, 1.2f);
            DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial = panelMat;

            var lgo = new GameObject($"{namePrefix}_{i}");
            lgo.transform.SetParent(holder.transform, false);
            lgo.transform.localPosition = pos + Vector3.down * 0.4f;
            var light   = lgo.AddComponent<Light>();
            light.type  = LightType.Point;
            light.color = new Color(0.8f, 0.85f, 1f);
            light.range = roomHeight * 2f;
            var hd = lgo.AddComponent<HDAdditionalLightData>();
            hd.SetIntensity(1100f, LightUnit.Lumen);
            hd.EnableShadows(false);
            light.enabled = false;   // karanlik sekans: bastan sonuk
        }
    }

    // Script ekleyecegin bos konum isareti. forward verilirse o yone bakar.
    void Marker(Transform parent, string name, Vector3 localPos, Vector3 forward = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        if (forward != default)
            go.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    Material Lit(Color c, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }
}
}
