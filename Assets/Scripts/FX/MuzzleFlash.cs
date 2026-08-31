using UnityEngine;

namespace Bloodrush.FX
{
// Kısa ömürlü namlu flaşı — hiç asset gerektirmez. Parlak emissive küre (HDR renk,
// Bloom yakalar) hızla küçülüp yok olur. Düşman menzilli ateş edince "ateş etti"
// göstergesi olarak muzzle konumunda çakar. Statik (rigsiz) silah modelleri için
// prosedürel ateş geri bildirimi.
public class MuzzleFlash : MonoBehaviour
{
    static Material sharedMat;

    // pos: dünya konumu. parent: verilirse flaş ona bağlanır (namluyla birlikte hareket eder).
    public static void Spawn(Vector3 pos, Transform parent = null, float size = 0.28f, float life = 0.05f)
    {
        var go = new GameObject("MuzzleFlash");
        go.transform.position = pos;
        if (parent) go.transform.SetParent(parent, true);
        go.AddComponent<MuzzleFlash>().Init(size, life);
    }

    Transform vis;
    float     startSize, life, age;

    void Init(float size, float lifeTime)
    {
        startSize = size;
        life      = lifeTime;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());     // mermi/kanca takılmasın
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * size;
        sphere.GetComponent<Renderer>().sharedMaterial = GetMat();
        vis = sphere.transform;
    }

    // Tek paylaşımlı emissive materyal (atış başına material sızıntısı olmasın)
    static Material GetMat()
    {
        if (sharedMat == null)
        {
            var sh = Shader.Find("HDRP/Unlit");
            sharedMat = new Material(sh);
            sharedMat.SetColor("_UnlitColor", new Color(1f, 0.7f, 0.25f) * 2.5f);   // hafif HDR turuncu (Bloom patlatmasın)
        }
        return sharedMat;
    }

    void Update()
    {
        age += Time.deltaTime;
        float k = 1f - age / life;
        if (k <= 0f) { Destroy(gameObject); return; }
        if (vis) vis.localScale = Vector3.one * startSize * k;   // hızla küçülerek söner
    }
}
}
