using UnityEngine;

namespace Bloodrush.Flow
{
// Devrilmis rafin uzerine/etrafina dagilmis laboratuvar esyalari uretir (greybox).
// Bos bir objeye ekle, sonra dis menusunden "Bilimsel Esyalari Kur".
//
// RAFIN CHILD'I yaparsan esyalar raf itilince onunla birlikte hareket eder. Yere sacilmis,
// rafla birlikte kaymamasi gereken parcalar istiyorsan ayri bir obje olarak birak.
//
// Sivilar HDRP/Unlit ve parlak renkli: karanlik sekansta zifiri bir odada okunabilir renk
// lekeleri birakiyorlar — greybox'ta bile "burada bir laboratuvar vardi" hissini veren sey bu.
public class LabPropScatter : MonoBehaviour
{
    [Header("Dagilim")]
    [SerializeField] int   count    = 14;
    [Tooltip("Esyalarin sacilacagi alan (X, Y, Z). Y > 0 ise farkli yuksekliklere de dagilir.")]
    [SerializeField] Vector3 areaSize = new Vector3(2.6f, 0.2f, 1.0f);
    [Tooltip("Ayni sayi = ayni dagilim. Begenmedigin dizilimi degistirmek icin arttir.")]
    [SerializeField] int   seed      = 12345;
    [Tooltip("Kacinin devrilmis/yatik duracagi (0 = hepsi dik, 1 = hepsi devrik).")]
    [Range(0f, 1f)]
    [SerializeField] float tippedRatio = 0.55f;
    [Tooltip("Boyut cesitliligi. 0.2 = %20 buyuyup kuculebilir.")]
    [Range(0f, 0.6f)]
    [SerializeField] float sizeJitter = 0.25f;

    [Header("Renkler")]
    [SerializeField] Color glassColor = new Color(0.75f, 0.82f, 0.85f);
    [SerializeField] Color metalColor = new Color(0.24f, 0.25f, 0.27f);
    [SerializeField] Color paperColor = new Color(0.78f, 0.75f, 0.68f);
    [Tooltip("Siselerin icindeki sivi renkleri — sirayla/rastgele dagitilir.")]
    [SerializeField] Color[] liquidColors = {
        new Color(0.35f, 1f, 0.45f),    // yesil
        new Color(1f, 0.72f, 0.2f),     // amber
        new Color(0.35f, 0.7f, 1f),     // mavi
        new Color(1f, 0.35f, 0.55f),    // pembe
    };

    Material glassMat, metalMat, paperMat;
    Material[] liquidMats;

    [ContextMenu("Bilimsel Esyalari Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        glassMat = Lit(glassColor, 0.05f, 0.92f);
        metalMat = Lit(metalColor, 0.8f,  0.4f);
        paperMat = Lit(paperColor, 0f,    0.15f);

        liquidMats = new Material[Mathf.Max(1, liquidColors.Length)];
        for (int i = 0; i < liquidMats.Length; i++)
        {
            var m = new Material(Shader.Find("HDRP/Unlit"));
            Color c = liquidColors.Length > 0 ? liquidColors[i % liquidColors.Length] : Color.green;
            m.SetColor("_UnlitColor", c * 1.8f);   // karanlikta hafif parlasin
            liquidMats[i] = m;
        }

        // Deterministik dagilim: disaridaki Random akisini bozmadan seed uygula.
        var prevState = Random.state;
        Random.InitState(seed);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-areaSize.x, areaSize.x) * 0.5f,
                Random.Range(0f, areaSize.y),
                Random.Range(-areaSize.z, areaSize.z) * 0.5f);

            bool tipped = Random.value < tippedRatio;
            float scale = 1f + Random.Range(-sizeJitter, sizeJitter);

            var item = new GameObject($"Esya_{i}");
            item.transform.SetParent(transform, false);
            item.transform.localPosition = pos;
            item.transform.localRotation = tipped
                ? Quaternion.Euler(Random.Range(70f, 110f), Random.Range(0f, 360f), Random.Range(-25f, 25f))
                : Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-6f, 6f));
            item.transform.localScale = Vector3.one * scale;

            switch (Random.Range(0, 5))
            {
                case 0: MakeTestTube(item.transform); break;
                case 1: MakeFlask(item.transform);    break;
                case 2: MakeJar(item.transform);      break;
                case 3: MakeCrate(item.transform);    break;
                default: MakeFolder(item.transform);  break;
            }
        }

        Random.state = prevState;
        Debug.Log($"[LabPropScatter] {count} esya kuruldu (seed {seed}).", this);
    }

    // ── Esya tipleri ───────────────────────────────────────────────────

    void MakeTestTube(Transform p)
    {
        Prim(p, "Tup", PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0f),
             new Vector3(0.035f, 0.09f, 0.035f), glassMat);
        Prim(p, "Sivi", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f),
             new Vector3(0.028f, 0.045f, 0.028f), RandomLiquid());
    }

    void MakeFlask(Transform p)
    {
        Prim(p, "Govde", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f),
             new Vector3(0.07f, 0.07f, 0.07f), glassMat);
        Prim(p, "Boyun", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f),
             new Vector3(0.028f, 0.045f, 0.028f), glassMat);
        Prim(p, "Sivi", PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f),
             new Vector3(0.062f, 0.04f, 0.062f), RandomLiquid());
    }

    void MakeJar(Transform p)
    {
        Prim(p, "Kavanoz", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f),
             new Vector3(0.09f, 0.06f, 0.09f), glassMat);
        Prim(p, "Kapak", PrimitiveType.Cylinder, new Vector3(0f, 0.13f, 0f),
             new Vector3(0.095f, 0.012f, 0.095f), metalMat);
    }

    void MakeCrate(Transform p)
    {
        Prim(p, "Kutu", PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f),
             new Vector3(0.22f, 0.14f, 0.16f), metalMat);
    }

    void MakeFolder(Transform p)
    {
        int sheets = Random.Range(2, 5);
        for (int i = 0; i < sheets; i++)
            Prim(p, $"Dosya_{i}", PrimitiveType.Cube,
                 new Vector3(Random.Range(-0.02f, 0.02f), 0.008f + i * 0.014f, Random.Range(-0.02f, 0.02f)),
                 new Vector3(0.2f, 0.012f, 0.26f), paperMat,
                 Quaternion.Euler(0f, Random.Range(-14f, 14f), 0f));
    }

    // ── Yardimcilar ────────────────────────────────────────────────────

    Material RandomLiquid() => liquidMats[Random.Range(0, liquidMats.Length)];

    void Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale,
              Material mat, Quaternion rot = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot == default ? Quaternion.identity : rot;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Kucuk dekor parcalarinin collider'i yok: oyuncu takilmasin, fizik maliyeti olmasin.
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
