using UnityEngine;

namespace Bloodrush.Flow
{
// Laboratuvar rafi (greybox). ReadingCornerBuilder.BuildShelfUnit ile AYNI iskelet —
// arka panel + yan cerceveler + ust + yatay raflar — ama ahsap kitaplik yerine metal
// laboratuvar rafi, ve raflarda kitap yerine dagilmis bilimsel esyalar.
//
// Bos bir objeye ekle, sonra dis menusunden "Laboratuvar Rafi Kur".
//
// Devrilmis raf istiyorsan: bu objeyi DIK kur, sonra PARENT objesini devir. Esyalar child
// oldugu icin onunla birlikte doner (rafa sikismis esya hissi). Yere sacilmis, rafla
// birlikte donmemesi gereken parcalar icin ayrica LabPropScatter kullan.
//
// NOT: prop yardimcilari LabPropScatter ile benziyor. Bu projede her builder kendi
// yardimcilarinin kopyasini tasiyor (RoomBuilder / GravityRoomBuilder / ReceptionBuilder
// hepsi oyle) — bilerek DRY degil, her builder tek basina ayakta dursun diye.
public class LabShelfBuilder : MonoBehaviour
{
    [Header("Raf olculeri (metre)")]
    [SerializeField] float width  = 1.8f;
    [SerializeField] float height = 2.0f;
    [SerializeField] float depth  = 0.5f;
    [Tooltip("Kac yatay raf kati olsun.")]
    [SerializeField] int   rows   = 4;
    [Tooltip("Panel ve raf kalinligi.")]
    [SerializeField] float boardThickness = 0.04f;
    [Tooltip("Arka panel olsun mu? Kapatirsan raf acik iskelet olur.")]
    [SerializeField] bool  backPanel = true;

    [Header("Esyalar")]
    [Tooltip("Her raf katina kac esya. 0 = bos raf.")]
    [SerializeField] int   propsPerRow = 4;
    [Tooltip("Ayni sayi = ayni dizilim. Begenmedigin dagilimi degistirmek icin arttir.")]
    [SerializeField] int   seed        = 4242;
    [Range(0f, 1f)]
    [Tooltip("Kacinin devrik duracagi — daginiklik hissi.")]
    [SerializeField] float tippedRatio = 0.4f;

    [Header("Renkler")]
    [SerializeField] Color shelfColor = new Color(0.26f, 0.27f, 0.29f);   // metal raf
    [SerializeField] Color glassColor = new Color(0.75f, 0.82f, 0.85f);
    [SerializeField] Color metalColor = new Color(0.22f, 0.23f, 0.25f);
    [SerializeField] Color paperColor = new Color(0.78f, 0.75f, 0.68f);
    [Tooltip("Sise iclerindeki sivi renkleri — karanlikta hafif parlar.")]
    [SerializeField] Color[] liquidColors = {
        new Color(0.35f, 1f, 0.45f),
        new Color(1f, 0.72f, 0.2f),
        new Color(0.35f, 0.7f, 1f),
        new Color(1f, 0.35f, 0.55f),
    };

    Material shelfMat, glassMat, metalMat, paperMat;
    Material[] liquidMats;

    [ContextMenu("Laboratuvar Rafi Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        shelfMat = Lit(shelfColor, 0.7f,  0.35f);
        glassMat = Lit(glassColor, 0.05f, 0.92f);
        metalMat = Lit(metalColor, 0.8f,  0.4f);
        paperMat = Lit(paperColor, 0f,    0.15f);

        liquidMats = new Material[Mathf.Max(1, liquidColors.Length)];
        for (int i = 0; i < liquidMats.Length; i++)
        {
            var m = new Material(Shader.Find("HDRP/Unlit"));
            Color c = liquidColors.Length > 0 ? liquidColors[i % liquidColors.Length] : Color.green;
            m.SetColor("_UnlitColor", c * 1.8f);
            liquidMats[i] = m;
        }

        var prev = Random.state;
        Random.InitState(seed);

        BuildFrame();

        int r = Mathf.Max(1, rows);
        float innerW = width - 0.16f;
        for (int i = 0; i < r; i++)
        {
            float y = height * i / r + boardThickness * 0.5f;
            if (propsPerRow > 0) ScatterOnShelf(y, innerW);
        }

        Random.state = prev;
        Debug.Log($"[LabShelfBuilder] Raf kuruldu ({r} kat, seed {seed}).", this);
    }

    // ── Iskelet (ReadingCornerBuilder.BuildShelfUnit deseni) ───────────

    void BuildFrame()
    {
        var frame = new GameObject("RafIskeleti").transform;
        frame.SetParent(transform, false);

        if (backPanel)
            Box(frame, "ArkaPanel", new Vector3(0f, height * 0.5f, -depth * 0.45f),
                new Vector3(width, height, 0.03f), shelfMat);

        Box(frame, "YanSol", new Vector3(-width * 0.5f, height * 0.5f, 0f),
            new Vector3(boardThickness, height, depth), shelfMat);
        Box(frame, "YanSag", new Vector3( width * 0.5f, height * 0.5f, 0f),
            new Vector3(boardThickness, height, depth), shelfMat);
        Box(frame, "Ust", new Vector3(0f, height, 0f),
            new Vector3(width, boardThickness, depth), shelfMat);

        int r = Mathf.Max(1, rows);
        for (int i = 0; i <= r; i++)
        {
            float y = height * i / r;
            Box(frame, $"Raf_{i}", new Vector3(0f, y, 0f),
                new Vector3(width, boardThickness, depth), shelfMat);
        }
    }

    // ── Raf ustundeki esyalar ──────────────────────────────────────────

    void ScatterOnShelf(float y, float innerW)
    {
        var row = new GameObject($"Esyalar_{y:0.00}").transform;
        row.SetParent(transform, false);
        row.localPosition = new Vector3(0f, y, 0f);

        for (int i = 0; i < propsPerRow; i++)
        {
            var item = new GameObject($"Esya_{i}");
            item.transform.SetParent(row, false);
            item.transform.localPosition = new Vector3(
                Random.Range(-innerW, innerW) * 0.5f, 0f,
                Random.Range(-depth * 0.28f, depth * 0.28f));

            bool tipped = Random.value < tippedRatio;
            item.transform.localRotation = tipped
                ? Quaternion.Euler(Random.Range(72f, 108f), Random.Range(0f, 360f), Random.Range(-20f, 20f))
                : Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-5f, 5f));
            item.transform.localScale = Vector3.one * (1f + Random.Range(-0.2f, 0.2f));

            switch (Random.Range(0, 5))
            {
                case 0:  TestTube(item.transform); break;
                case 1:  Flask(item.transform);    break;
                case 2:  Jar(item.transform);      break;
                case 3:  Crate(item.transform);    break;
                default: Folder(item.transform);   break;
            }
        }
    }

    void TestTube(Transform p)
    {
        Cyl(p, "Tup",  new Vector3(0f, 0.09f,  0f), new Vector3(0.035f, 0.09f,  0.035f), glassMat);
        Cyl(p, "Sivi", new Vector3(0f, 0.05f,  0f), new Vector3(0.028f, 0.045f, 0.028f), Liquid());
    }

    void Flask(Transform p)
    {
        Cyl(p, "Govde", new Vector3(0f, 0.07f,  0f), new Vector3(0.07f,  0.07f,  0.07f),  glassMat);
        Cyl(p, "Boyun", new Vector3(0f, 0.17f,  0f), new Vector3(0.028f, 0.045f, 0.028f), glassMat);
        Cyl(p, "Sivi",  new Vector3(0f, 0.045f, 0f), new Vector3(0.062f, 0.04f,  0.062f), Liquid());
    }

    void Jar(Transform p)
    {
        Cyl(p, "Kavanoz", new Vector3(0f, 0.06f, 0f), new Vector3(0.09f,  0.06f,  0.09f),  glassMat);
        Cyl(p, "Kapak",   new Vector3(0f, 0.13f, 0f), new Vector3(0.095f, 0.012f, 0.095f), metalMat);
    }

    void Crate(Transform p)
    {
        Box(p, "Kutu", new Vector3(0f, 0.07f, 0f), new Vector3(0.2f, 0.14f, 0.15f), metalMat);
    }

    void Folder(Transform p)
    {
        int sheets = Random.Range(2, 5);
        for (int i = 0; i < sheets; i++)
            Box(p, $"Dosya_{i}",
                new Vector3(Random.Range(-0.02f, 0.02f), 0.008f + i * 0.014f, Random.Range(-0.02f, 0.02f)),
                new Vector3(0.18f, 0.012f, 0.24f), paperMat);
    }

    // ── Yardimcilar ────────────────────────────────────────────────────

    Material Liquid() => liquidMats[Random.Range(0, liquidMats.Length)];

    void Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        => Prim(parent, name, PrimitiveType.Cube, pos, scale, mat);

    void Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        => Prim(parent, name, PrimitiveType.Cylinder, pos, scale, mat);

    void Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Dekor parcalarinin collider'i yok: oyuncu takilmasin, fizik maliyeti olmasin.
        // Rafin yolu KAPATMASI, PushObstacle'in durdugu parent objenin collider'iyla olur.
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
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
