using UnityEngine;

// Greybox merdiven kurucu: boş bir GO'ya ekle, konumunu merdivenin BAŞLAYACAĞI
// yere koy (duvardan uzağa, açık alana), GO'yu podyuma dönük çevir (+Z yönü
// merdivenin çıkış yönü), ⋮ menüsünden "Merdiven Kur" seç.
// Her basamak zemine kadar dolu bir blok → oyuncu güvenle çıkar (boşluk yok).
public class StairBuilder : MonoBehaviour
{
    [Header("Basamak")]
    [SerializeField] int   stepCount = 5;      // basamak sayısı
    [SerializeField] float stepWidth = 3f;     // genişlik (X)
    [SerializeField] float stepHeight = 0.3f;  // her basamağın yüksekliği (Y)
    [SerializeField] float stepDepth = 0.5f;   // her basamağın derinliği (Z)

    [ContextMenu("Merdiven Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        for (int i = 0; i < stepCount; i++)
        {
            float topY = (i + 1) * stepHeight;                  // bu basamağın üst yüzeyi
            float z    = i * stepDepth + stepDepth * 0.5f;      // ileri konum

            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Basamak_{i}";
            step.transform.SetParent(transform, false);
            step.transform.localPosition = new Vector3(0f, topY * 0.5f, z);
            step.transform.localScale    = new Vector3(stepWidth, topY, stepDepth);
            step.isStatic = true;   // NavMesh bake için
        }
    }
}
