using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Bloodrush.Flow
{
// "Aşırı Yük" odasının içini greybox olarak kurar. PuzzleRoomBuilder / ReceptionBuilder /
// LabShelfBuilder ile AYNI desen: kendi container child'ı, [ContextMenu], her çalıştırmada
// DestroyImmediate ile kendi çocuklarını silip yeniden kurar, yardımcılarının kendi
// kopyasını taşır (kasıtlı olarak DRY değil — her builder self-contained).
//
// ÖNEMLİ: Bu builder mevcut CH3 "Puzzle Room" KABUĞUNUN İÇİNE kurar. Kabuğa (Zemin, Tavan,
// Duvar_*, TavanIsiklari, CezaSpawnlari) HİÇ DOKUNMAZ — her şeyi tek bir "AsiriYuk"
// child'ı altına koyar. Bu objeye ekleyip ⋮ "Aşırı Yük Odasını Kur" de.
//
// Kabuğun ölçüleri sahneden okundu: zemin 41 x 21, yükseklik 8, doğu kapı boşluğu
// local x +20.2'de 4 m genişlik x 4 m yükseklik, batı tarafı açık (duvar kurulmamış).
public class OverloadRoomBuilder : MonoBehaviour
{
    [Header("Oda ölçüleri (mevcut kabuğa göre, local)")]
    [Tooltip("İç duvarların yarı genişliği (x ekseni).")]
    [SerializeField] float halfWidth = 20f;
    [Tooltip("İç duvarların yarı derinliği (z ekseni).")]
    [SerializeField] float halfDepth = 10f;
    [SerializeField] float roomHeight = 8f;
    [Tooltip("Zemin üst yüzeyinin local Y'si.")]
    [SerializeField] float floorY = 0.05f;

    [Header("Kapılar")]
    [SerializeField] float exitX          = 20.2f;
    [SerializeField] float exitGapWidth   = 4f;
    [SerializeField] float exitDoorHeight = 4f;
    [SerializeField] float entryX         = -20.2f;

    [Header("Şarj bölgesi")]
    [Tooltip("Bölgenin kenar uzunluğu (kare). 8 = 8x8 m.")]
    [SerializeField] float zoneSize = 8f;
    [SerializeField] float zoneHeight = 4f;
    [SerializeField] float barWidth = 0.35f;
    [SerializeField] float postHeight = 1.2f;
    [SerializeField] Color zoneEmpty = new Color(0.9f, 0.25f, 0.15f);

    [Header("Jeneratör")]
    [SerializeField] Vector3 generatorPos = new Vector3(16.5f, 0f, 0f);
    [SerializeField] int     segmentCount = 10;

    [Header("Spawn noktaları")]
    [Tooltip("Bölgeden uzakta, oda çevresine dağıtılır.")]
    [SerializeField] Vector2[] spawnXZ =
    {
        new Vector2(-15f,  7.5f), new Vector2(-15f, -7.5f),
        new Vector2( 12f,  7.5f), new Vector2( 12f, -7.5f),
        new Vector2(  0f,  8.5f), new Vector2(  0f, -8.5f),
    };

    const string Container = "AsiriYuk";

    Material emissiveMat;   // halka için (tek örnek, ChargeZone runtime'da kopyalar)
    Material darkMat;       // segment "sönük" hâli için taban

    [ContextMenu("Aşırı Yük Odasını Kur")]
    void Build()
    {
        var old = transform.Find(Container);
        if (old != null) DestroyImmediate(old.gameObject);

        var root = new GameObject(Container);
        root.transform.SetParent(transform, false);

        emissiveMat = new Material(Shader.Find("HDRP/Unlit"));
        emissiveMat.SetColor("_UnlitColor", zoneEmpty * 2.5f);

        darkMat = new Material(Shader.Find("HDRP/Unlit"));
        darkMat.SetColor("_UnlitColor", new Color(0.08f, 0.08f, 0.09f));

        var zone      = BuildChargeZone(root.transform);
        var generator = BuildGenerator(root.transform);
        var entry     = BuildEntryBarrier(root.transform);
        var exit      = BuildExitBarrier(root.transform);
        var spawns    = BuildSpawnPoints(root.transform);

        // Denetleyici + oda hacmi trigger'ı
        var ctrlGO = new GameObject("OverloadRoom");
        ctrlGO.transform.SetParent(root.transform, false);
        var trig = ctrlGO.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0f, roomHeight * 0.5f, 0f);
        trig.size   = new Vector3(halfWidth * 2f - 2f, roomHeight, halfDepth * 2f - 2f);
        var room = ctrlGO.AddComponent<OverloadRoom>();
        room.Configure(zone, generator, entry, exit, spawns);

        Debug.Log("[OverloadRoomBuilder] Aşırı Yük odası kuruldu. Sırada: OverloadRoom " +
                  "bileşenine 'Enemy Prefab' olarak KANCAYLA ÇEKİLEBİLEN küçük düşmanı " +
                  "(düşman1) ata.", this);
    }

    // ───────────────── Şarj bölgesi ─────────────────

    ChargeZone BuildChargeZone(Transform parent)
    {
        var go = new GameObject("SarjBolgesi");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, floorY, 0f);

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size      = new Vector3(zoneSize, zoneHeight, zoneSize);
        box.center    = new Vector3(0f, zoneHeight * 0.5f, 0f);

        var parts = new List<Renderer>();
        float h = zoneSize * 0.5f;

        // Kenar çubukları — zeminde parlayan kare çerçeve
        parts.Add(MakeEmissive(go.transform, "Kenar_Kuzey", new Vector3(0f, 0.03f,  h), new Vector3(zoneSize, 0.06f, barWidth)));
        parts.Add(MakeEmissive(go.transform, "Kenar_Guney", new Vector3(0f, 0.03f, -h), new Vector3(zoneSize, 0.06f, barWidth)));
        parts.Add(MakeEmissive(go.transform, "Kenar_Dogu",  new Vector3( h, 0.03f, 0f), new Vector3(barWidth, 0.06f, zoneSize)));
        parts.Add(MakeEmissive(go.transform, "Kenar_Bati",  new Vector3(-h, 0.03f, 0f), new Vector3(barWidth, 0.06f, zoneSize)));

        // Köşe direkleri — çatışma sırasında bölgeyi uzaktan görebilmek için
        foreach (var s in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
            parts.Add(MakeEmissive(go.transform, $"Direk_{s.x}_{s.y}",
                new Vector3(s.x * h, postHeight * 0.5f, s.y * h),
                new Vector3(0.18f, postHeight, 0.18f)));

        var zone = go.AddComponent<ChargeZone>();
        zone.Configure(parts.ToArray());
        return zone;
    }

    // ───────────────── Jeneratör ─────────────────

    OverloadGenerator BuildGenerator(Transform parent)
    {
        var go = new GameObject("Jenerator");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(generatorPos.x, floorY + generatorPos.y, generatorPos.z);

        // Gövde (katı — oyuncu içinden geçemesin)
        MakeSolid(go.transform, "Govde", new Vector3(0f, 1.2f, 0f), new Vector3(2f, 2.4f, 1.6f));
        MakeSolid(go.transform, "Taban", new Vector3(0f, 0.15f, 0f), new Vector3(2.6f, 0.3f, 2.2f));

        // Işıklı yüz oda tarafına (−X) baksın
        var glow = MakeEmissive(go.transform, "Glow", new Vector3(-1.05f, 1.9f, 0f), new Vector3(0.08f, 0.5f, 1.4f));

        // Segment çubuğu — soldan sağa dolar
        var segs = new Renderer[Mathf.Max(1, segmentCount)];
        float segW = 1.4f / segs.Length;
        for (int i = 0; i < segs.Length; i++)
        {
            float z = -0.7f + segW * (i + 0.5f);
            segs[i] = MakeEmissive(go.transform, $"Segment_{i}",
                new Vector3(-1.05f, 1.0f, z),
                new Vector3(0.08f, 0.22f, segW * 0.8f), darkMat);
        }

        // Işık
        var lgo = new GameObject("Isik");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localPosition = new Vector3(-1.6f, 2.2f, 0f);
        var light   = lgo.AddComponent<Light>();
        light.type  = LightType.Point;
        light.range = 12f;
        var hd = lgo.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(200f, LightUnit.Lumen);
        hd.EnableShadows(false);

        // Uğultu (klip KULLANICI atar — boşsa sessiz çalışır)
        var hum = go.AddComponent<AudioSource>();
        hum.playOnAwake   = false;
        hum.loop          = true;
        hum.spatialBlend  = 1f;
        hum.minDistance   = 3f;
        hum.maxDistance   = 25f;
        hum.volume        = 0.15f;

        // Kıvılcım
        var sgo = new GameObject("Kivilcim");
        sgo.transform.SetParent(go.transform, false);
        sgo.transform.localPosition = new Vector3(-1.1f, 2.4f, 0f);
        var ps = sgo.AddComponent<ParticleSystem>();
        ConfigureSparks(ps);

        var gen = go.AddComponent<OverloadGenerator>();
        gen.Configure(glow, segs, light, hum, ps);
        return gen;
    }

    void ConfigureSparks(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime    = 0.45f;
        main.startSpeed       = 3.5f;
        main.startSize        = 0.05f;
        main.startColor       = new Color(1f, 0.75f, 0.3f);
        main.gravityModifier  = 1.4f;
        main.playOnAwake      = false;
        main.maxParticles     = 60;

        var em = ps.emission;
        em.rateOverTime = 18f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 32f;
        sh.radius    = 0.12f;
        sh.rotation  = new Vector3(0f, -90f, 0f);   // oda tarafına saçsın

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 2.5f;
            rend.sharedMaterial = emissiveMat;
        }
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ───────────────── Kapılar ─────────────────

    GameObject BuildEntryBarrier(Transform parent)
    {
        var go = MakeSolid(parent, "EntryBarrier",
            new Vector3(entryX, floorY + roomHeight * 0.5f, 0f),
            new Vector3(0.5f, roomHeight, halfDepth * 2f + 1f));
        go.SetActive(false);   // oyuncu girene kadar AÇIK
        return go;
    }

    GameObject BuildExitBarrier(Transform parent)
    {
        var go = MakeSolid(parent, "ExitBarrier",
            new Vector3(exitX, floorY + exitDoorHeight * 0.5f, 0f),
            new Vector3(0.5f, exitDoorHeight, exitGapWidth));
        go.SetActive(true);    // şarj dolana kadar KAPALI
        return go;
    }

    Transform[] BuildSpawnPoints(Transform parent)
    {
        var holder = new GameObject("SpawnNoktalari");
        holder.transform.SetParent(parent, false);

        var list = new Transform[spawnXZ.Length];
        for (int i = 0; i < spawnXZ.Length; i++)
        {
            var go = new GameObject($"Spawn_{i}");
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = new Vector3(spawnXZ[i].x, floorY + 0.1f, spawnXZ[i].y);
            // Odanın merkezine baksın
            go.transform.localRotation = Quaternion.LookRotation(
                new Vector3(-spawnXZ[i].x, 0f, -spawnXZ[i].y).normalized, Vector3.up);
            list[i] = go.transform;
        }
        return list;
    }

    // ───────────────── Yardımcılar (bu builder'a özel kopyalar) ─────────────────

    GameObject MakeSolid(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        return go;
    }

    Renderer MakeEmissive(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        DestroyImmediate(go.GetComponent<Collider>());   // ışık paneli engel olmasın
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat != null ? mat : emissiveMat;
        return r;
    }
}
}
