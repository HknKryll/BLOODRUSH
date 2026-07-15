using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Greybox oda kurucu: boş bir GO'ya ekle, Inspector'da boyutları ayarla,
// sağ üstteki ⋮ menüsünden "Odayı Kur" seç. Zemin + tavan + 4 duvarı
// ayrı küpler olarak child yapar (içeriden görünür, collider'lar doğru)
// ve tavana otomatik point light ızgarası döşer.
// Kapı boşluğu için: kurulduktan sonra istediğin duvarı silip yerine
// iki parça duvar koy veya duvarı kapı hizasında ölçekle.
public class RoomBuilder : MonoBehaviour
{
    [Header("Oda Ölçüleri (metre)")]
    [SerializeField] float width  = 30f;   // X
    [SerializeField] float height = 6f;    // Y
    [SerializeField] float depth  = 15f;   // Z
    [SerializeField] float wallThickness = 0.5f;
    [SerializeField] bool  buildCeiling   = true;

    [Header("Işıklar")]
    [SerializeField] bool  buildLights    = true;
    [SerializeField] Color lightColor     = new Color(1f, 0.95f, 0.85f); // hafif sıcak beyaz
    [SerializeField] float lightLumen     = 1500f;   // ilk sıcak görünüm; Ch5_Yeralti için ~500 + kırmızı ton
    [SerializeField] float lightSpacing   = 8f;      // ışıklar arası mesafe

    [ContextMenu("Odayı Kur")]
    void Build()
    {
        // Eski parçaları temizle
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        float t = wallThickness;

        MakeSlab("Zemin",  new Vector3(0f, -t * 0.5f, 0f),          new Vector3(width + t * 2f, t, depth + t * 2f));
        if (buildCeiling)
            MakeSlab("Tavan", new Vector3(0f, height + t * 0.5f, 0f), new Vector3(width + t * 2f, t, depth + t * 2f));

        MakeSlab("Duvar_Kuzey", new Vector3(0f, height * 0.5f,  depth * 0.5f + t * 0.5f), new Vector3(width + t * 2f, height, t));
        MakeSlab("Duvar_Guney", new Vector3(0f, height * 0.5f, -depth * 0.5f - t * 0.5f), new Vector3(width + t * 2f, height, t));
        MakeSlab("Duvar_Dogu",  new Vector3( width * 0.5f + t * 0.5f, height * 0.5f, 0f), new Vector3(t, height, depth));
        MakeSlab("Duvar_Bati",  new Vector3(-width * 0.5f - t * 0.5f, height * 0.5f, 0f), new Vector3(t, height, depth));

        if (buildLights) BuildLights();
    }

    void BuildLights()
    {
        int countX = Mathf.Max(1, Mathf.RoundToInt(width / lightSpacing));
        int countZ = Mathf.Max(1, Mathf.RoundToInt(depth / lightSpacing));

        float stepX = width / countX;
        float stepZ = depth / countZ;
        float y     = height - 0.4f;   // tavanın hemen altı

        for (int x = 0; x < countX; x++)
        for (int z = 0; z < countZ; z++)
        {
            Vector3 pos = new Vector3(
                -width * 0.5f + stepX * (x + 0.5f),
                y,
                -depth * 0.5f + stepZ * (z + 0.5f));
            MakeLight($"Isik_{x}_{z}", pos);
        }
    }

    void MakeLight(string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        var light   = go.AddComponent<Light>();
        light.type  = LightType.Point;
        light.color = lightColor;
        light.range = Mathf.Max(height, lightSpacing) * 1.3f;

        var hd = go.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(lightLumen, LightUnit.Lumen);
        hd.EnableShadows(false);   // greybox'ta performans için gölgesiz
    }

    void MakeSlab(string name, Vector3 localPos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        go.isStatic = true;   // NavMesh bake için
    }
}
