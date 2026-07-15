using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Sadece ışık koyar — DUVARLARA/GEOMETRİYE HİÇ DOKUNMAZ.
// Var olan bir odaya/koridora ışık eklemek için: boş GO'yu odanın yatay
// merkezine (zemin hizasına) koy, boyutları ayarla, ⋮ menü → "Işıkları Kur".
// Sadece kendi ışık child'larını yönetir; tekrar çalıştırmak güvenli.

namespace Bloodrush.Flow
{
public class LightGrid : MonoBehaviour
{
    public enum Layout
    {
        KareIzgara,      // dikdörtgen odalar: tavanda ızgara
        DaireselTavan,   // dairesel arena: tavanda halkalar
        DuvarHalkasi     // dairesel arena: ışıklar ÇEVRE DUVARINDA (alçak koy → zemin aydınlanır)
    }

    [Header("Yerleşim")]
    [SerializeField] Layout layout = Layout.KareIzgara;

    [Header("Alan (metre)")]
    [SerializeField] float width       = 20f;    // dairesel modlarda çap olarak kullanılır
    [SerializeField] float depth       = 20f;
    [SerializeField] float lightHeight = 5.5f;   // ışıkların yüksekliği (DuvarHalkasi için duvardaki yükseklik, örn. 3)
    [SerializeField] float spacing     = 8f;
    [SerializeField] float wallInset   = 1.2f;   // DuvarHalkasi: duvardan içeri mesafe

    [Header("Işık")]
    [SerializeField] Color color           = new Color(1f, 0.95f, 0.85f); // sıcak beyaz
    [SerializeField] float lumen           = 3500f;   // mavi ortam ışığını yenmesi için güçlü
    [SerializeField] float rangeMultiplier = 1.6f;
    [SerializeField] bool  shadows         = false;

    [ContextMenu("Işıkları Kur")]
    void Build()
    {
        // Sadece KENDİ ışıklarını temizle (oda geometrisine dokunmaz)
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        switch (layout)
        {
            case Layout.DaireselTavan: BuildCircular(); break;
            case Layout.DuvarHalkasi:  BuildWallRing(); break;
            default:                   BuildGrid();     break;
        }
    }

    // Işıklar çevre duvarı boyunca tek halka — duvara monte lamba hissi.
    // lightHeight alçaksa (2.5-3.5) en alt zemin halkası da net aydınlanır.
    void BuildWallRing()
    {
        float r = Mathf.Min(width, depth) * 0.5f - wallInset;
        int count = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * r / spacing));
        for (int i = 0; i < count; i++)
        {
            float ang = (float)i / count * 2f * Mathf.PI;
            Vector3 pos = new Vector3(Mathf.Sin(ang) * r, lightHeight, Mathf.Cos(ang) * r);
            MakeLight($"DuvarIsik_{i}", pos);
        }
    }

    void BuildGrid()
    {
        int cx = Mathf.Max(1, Mathf.RoundToInt(width / spacing));
        int cz = Mathf.Max(1, Mathf.RoundToInt(depth / spacing));
        float sx = width / cx;
        float sz = depth / cz;

        for (int x = 0; x < cx; x++)
        for (int z = 0; z < cz; z++)
        {
            Vector3 pos = new Vector3(
                -width * 0.5f + sx * (x + 0.5f),
                lightHeight,
                -depth * 0.5f + sz * (z + 0.5f));
            MakeLight($"Isik_{x}_{z}", pos);
        }
    }

    // Dairesel: merkez ışık + artan yarıçaplı halkalar (dairesel arenayı takip eder)
    void BuildCircular()
    {
        float maxR = Mathf.Min(width, depth) * 0.5f;
        MakeLight("Isik_Merkez", new Vector3(0f, lightHeight, 0f));

        int rings = Mathf.Max(1, Mathf.RoundToInt(maxR / spacing));
        for (int r = 1; r <= rings; r++)
        {
            float rr    = maxR * (float)r / rings;
            int   count = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * rr / spacing));
            for (int i = 0; i < count; i++)
            {
                float ang = (float)i / count * 2f * Mathf.PI;
                Vector3 pos = new Vector3(Mathf.Sin(ang) * rr, lightHeight, Mathf.Cos(ang) * rr);
                MakeLight($"Isik_{r}_{i}", pos);
            }
        }
    }

    [ContextMenu("Işıkları Temizle")]
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    void MakeLight(string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        var light   = go.AddComponent<Light>();
        light.type  = LightType.Point;
        light.color = color;
        light.range = Mathf.Max(lightHeight, spacing) * rangeMultiplier;

        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(lumen, LightUnit.Lumen);
        hd.EnableShadows(shadows);
    }
}
}
