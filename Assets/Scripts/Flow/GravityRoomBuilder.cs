using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Yerçekimi odası greybox'ı (IndustrialHall/PuzzleRoom deseni). Boş GO'ya ekle →
// ⋮ "Yerçekimi Odası Kur". Set-piece: oda geneli DÜŞÜK yerçekimi (floaty); ortada
// bir UÇURUM var; oyuncu updraft (anti-grav) kolonlarıyla + floaty zıplayışla
// uçurumu geçip karşı çıkışa ulaşır. Girişte Konsey comms (kullanıcı bağlar),
// çıkışta karanlık-olay tetiği (sonraki faz).
namespace Bloodrush.Flow
{
public class GravityRoomBuilder : MonoBehaviour
{
    [Header("Oda (metre)")]
    [SerializeField] float width  = 44f;   // X (giriş batı → çıkış doğu)
    [SerializeField] float depth  = 26f;   // Z
    [SerializeField] float height = 8f;    // Y — flip'te zemin↔tavan yakın/okunur olsun (alçak)
    [SerializeField] float wallThickness = 0.5f;

    [Header("Uçurum (ortadaki boşluk)")]
    [SerializeField] float chasmWidth = 18f;   // X yönünde zemin boşluğu

    [Header("Kapı boşlukları")]
    [SerializeField] float entryGapWidth = 4f;
    [SerializeField] float exitGapWidth  = 4f;
    [SerializeField] float doorHeight    = 4f;

    [Header("Öğretici")]
    [Tooltip("AÇIK: flip-ÖĞRETİCİ oda — combat/asılı platform yok, uçurumu tavandan geçerek flip öğretilir, düşünce respawn. Asıl zorluk Flip Kulesi'nde. KAPALI: eski tam oda.")]
    [SerializeField] bool  tutorialMode = true;

    [Header("Yerçekimi")]
    [Tooltip("AÇIK: zıplayınca yerçekimi FLIP (tavan zemin olur). KAPALI: düşük-g + updraft kolonları.")]
    [SerializeField] bool  useFlip = true;
    [SerializeField] float roomGravityScale = 0.3f;   // oda geneli floaty (0.3, useFlip kapalıysa)
    [SerializeField] float updraftSpeed     = 13f;
    [SerializeField] int   updraftColumns   = 4;
    [SerializeField] float columnRadius     = 2.2f;

    [Header("Karmaşıklık (asılı platformlar)")]
    [SerializeField] int   platformCount = 5;

    Material emissiveMat;

    [ContextMenu("Yerçekimi Odası Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        emissiveMat = new Material(Shader.Find("HDRP/Unlit"));
        emissiveMat.SetColor("_UnlitColor", new Color(0.6f, 0.7f, 1f) * 2f);

        BuildShell();
        BuildFloors();
        if (!tutorialMode) BuildPlatforms();   // öğreticide asılı platform kalabalığı yok
        if (useFlip)
        {
            BuildFlipZone();          // zıplayınca flip (tavan zemin olur)
        }
        else
        {
            BuildLowGravZone();       // düşük-g + updraft kolonları
            BuildUpdraftColumns();
        }
        BuildBarrier("EntryBarrier", -width * 0.5f, entryGapWidth);
        BuildBarrier("ExitBarrier",  width * 0.5f, exitGapWidth);
        BuildCeilingLights();
        var spawnPoints = BuildMarkers();
        if (!tutorialMode) BuildSpawner(spawnPoints);   // öğreticide combat yok
        if (tutorialMode)  BuildTutorialRespawn();      // uçurum KillVolume + başlangıç checkpoint
    }

    // Öğretici: uçuruma düşünce son checkpoint'te canlan + başlangıç checkpoint'i
    void BuildTutorialRespawn()
    {
        var kill = new GameObject("KillVolume");
        kill.transform.SetParent(transform, false);
        kill.transform.localPosition = new Vector3(0f, -6f, 0f);   // uçurumun altı
        var kb = kill.AddComponent<BoxCollider>();
        kb.isTrigger = true;
        kb.size = new Vector3(chasmWidth + 2f, 2f, depth);
        kill.AddComponent<KillVolume>();

        var cp = new GameObject("Checkpoint");
        cp.transform.SetParent(transform, false);
        cp.transform.localPosition = new Vector3(-width * 0.5f + 5f, 1.5f, 0f);   // batı, spawn'ın hemen doğusu
        cp.transform.rotation = Quaternion.LookRotation(Vector3.right);
        var cb = cp.AddComponent<BoxCollider>();
        cb.isTrigger = true;
        cb.size = new Vector3(4f, 2.5f, 6f);
        cp.AddComponent<Checkpoint>();
    }

    // Uçurum üzerinde kademeli asılı platformlar (düşük-g'de zıplayarak geçilir + siper)
    void BuildPlatforms()
    {
        var parent = new GameObject("Platformlar");
        parent.transform.SetParent(transform, false);
        for (int i = 0; i < platformCount; i++)
        {
            float t = platformCount > 1 ? (float)i / (platformCount - 1) : 0.5f;
            float x = Mathf.Lerp(-chasmWidth * 0.42f, chasmWidth * 0.42f, t);
            float z = (i % 2 == 0) ? depth * 0.22f : -depth * 0.22f;
            float y = Mathf.Lerp(2.5f, height * 0.55f, (i % 3) / 2f);   // kademeli yükseklik
            MakeSlab($"Platform_{i}", new Vector3(x, y, z), new Vector3(4.5f, 0.4f, 4.5f), parent.transform);
        }
    }

    // Oyuncu odaya girince düşman doğuran spawner (oda-hacmi trigger'ı)
    void BuildSpawner(List<Transform> points)
    {
        var go = new GameObject("EnemySpawner");
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, height * 0.5f, 0f);
        box.size   = new Vector3(width - wallThickness, height, depth - wallThickness);
        go.AddComponent<RoomEnemySpawner>().Configure(points.ToArray());
    }

    // ───────── Kabuk ─────────

    void BuildShell()
    {
        float t = wallThickness;
        // Tavan kalın (2m) — flip'te oyuncu içinden tünellemesin; alt yüzü y=height'te kalır
        MakeSlab("Tavan", new Vector3(0f, height + 1f, 0f), new Vector3(width + t * 2f, 2f, depth + t * 2f));
        MakeSlab("Duvar_Kuzey", new Vector3(0f, height * 0.5f,  depth * 0.5f + t * 0.5f), new Vector3(width + t * 2f, height, t));
        MakeSlab("Duvar_Guney", new Vector3(0f, height * 0.5f, -depth * 0.5f - t * 0.5f), new Vector3(width + t * 2f, height, t));
        BuildGappedWall("Duvar_Bati", -width * 0.5f - t * 0.5f, entryGapWidth);
        BuildGappedWall("Duvar_Dogu",  width * 0.5f + t * 0.5f, exitGapWidth);
    }

    void BuildGappedWall(string name, float x, float gapWidth)
    {
        var parent = new GameObject(name);
        parent.transform.SetParent(transform, false);
        float t = wallThickness;
        float sideLen = (depth - gapWidth) * 0.5f;
        MakeSlab("Kanat_K", new Vector3(x, height * 0.5f,  (gapWidth + sideLen) * 0.5f), new Vector3(t, height, sideLen), parent.transform);
        MakeSlab("Kanat_G", new Vector3(x, height * 0.5f, -(gapWidth + sideLen) * 0.5f), new Vector3(t, height, sideLen), parent.transform);
        float lintelH = height - doorHeight;
        if (lintelH > 0.05f)
            MakeSlab("Lento", new Vector3(x, doorHeight + lintelH * 0.5f, 0f), new Vector3(t, lintelH, gapWidth), parent.transform);
    }

    // Zemin: batı platformu + doğu platformu, ortada uçurum
    void BuildFloors()
    {
        float t = wallThickness;
        float halfChasm = chasmWidth * 0.5f;
        float sideW = width * 0.5f - halfChasm;   // her kenar zemininin genişliği
        if (sideW <= 0.5f) return;

        float westCx = -(halfChasm + sideW * 0.5f);
        float eastCx =  (halfChasm + sideW * 0.5f);
        MakeSlab("Zemin_Bati", new Vector3(westCx, -t * 0.5f, 0f), new Vector3(sideW, t, depth + t * 2f));
        MakeSlab("Zemin_Dogu", new Vector3(eastCx, -t * 0.5f, 0f), new Vector3(sideW, t, depth + t * 2f));
        // Uçurum dibinde ölüm hacmi yok — düşük-g'de floaty; istersen KillVolume ekle
    }

    // Yerçekimi flip bölgesi (oda geneli) — zıplayınca tavan zemin olur
    void BuildFlipZone()
    {
        var go = new GameObject("FlipBolgesi");
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, height * 0.5f, 0f);
        box.size   = new Vector3(width - wallThickness, height, depth - wallThickness);
        go.AddComponent<GravityFlipZone>();
    }

    // Oda geneli düşük yerçekimi (büyük trigger)
    void BuildLowGravZone()
    {
        var go = new GameObject("DusukYercekimi");
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, height * 0.5f, 0f);
        box.size   = new Vector3(width - wallThickness, height, depth - wallThickness);
        go.AddComponent<GravityZone>().Configure(true, roomGravityScale, 0f);   // oda-geneli düşük-g
    }

    // Uçurumda yukarı itiş kolonları (updraft)
    void BuildUpdraftColumns()
    {
        var parent = new GameObject("UpdraftKolonlari");
        parent.transform.SetParent(transform, false);
        for (int i = 0; i < updraftColumns; i++)
        {
            float x = updraftColumns > 1
                ? Mathf.Lerp(-chasmWidth * 0.35f, chasmWidth * 0.35f, (float)i / (updraftColumns - 1))
                : 0f;
            var go = new GameObject($"Updraft_{i}");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, height * 0.5f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(columnRadius * 2f, height, columnRadius * 2f);
            go.AddComponent<GravityZone>().Configure(false, 1f, updraftSpeed);   // sadece itiş (yerçekimine dokunmaz)

            // Görsel: hafif parlak dikey sütun (collider'sız)
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vis.name = "Gorsel";
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(columnRadius * 1.6f, height * 0.5f, columnRadius * 1.6f);
            DestroyImmediate(vis.GetComponent<Collider>());
            vis.GetComponent<Renderer>().sharedMaterial = emissiveMat;
        }
    }

    // ───────── Bariyer / Işık / Marker ─────────

    GameObject BuildBarrier(string name, float x, float gapWidth)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(x, doorHeight * 0.5f, 0f);
        go.transform.localScale    = new Vector3(wallThickness, doorHeight, gapWidth);
        return go;
    }

    void BuildCeilingLights()
    {
        var parent = new GameObject("TavanIsiklari");
        parent.transform.SetParent(transform, false);
        var mat = new Material(Shader.Find("HDRP/Unlit"));
        var col = new Color(0.7f, 0.8f, 1f);
        mat.SetColor("_UnlitColor", col * 2f);
        float y = height - 0.25f;
        for (int cx = 0; cx < 3; cx++)
        for (int cz = 0; cz < 2; cz++)
        {
            Vector3 pos = new Vector3(-width * 0.5f + width * (cx + 0.5f) / 3f, y, -depth * 0.5f + depth * (cz + 0.5f) / 2f);
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = $"Panel_{cx}_{cz}";
            panel.transform.SetParent(parent.transform, false);
            panel.transform.localPosition = pos;
            panel.transform.localScale    = new Vector3(3f, 0.15f, 1.5f);
            DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial = mat;

            var lgo = new GameObject($"Isik_{cx}_{cz}");
            lgo.transform.SetParent(parent.transform, false);
            lgo.transform.localPosition = pos + Vector3.down * 0.6f;
            var light   = lgo.AddComponent<Light>();
            light.type  = LightType.Point;
            light.color = col;
            light.range = height * 1.4f;
            var hd = lgo.AddComponent<HDAdditionalLightData>();
            hd.SetIntensity(2800f, LightUnit.Lumen);
            hd.EnableShadows(false);
        }
    }

    List<Transform> BuildMarkers()
    {
        var parent = new GameObject("Markerlar");
        parent.transform.SetParent(transform, false);

        // Oyuncu girişi (batı zemin)
        var ps = new GameObject("PlayerStart");
        ps.transform.SetParent(parent.transform, false);
        ps.transform.localPosition = new Vector3(-width * 0.5f + 2f, 1.2f, 0f);
        ps.transform.localRotation = Quaternion.LookRotation(Vector3.right);
        ps.AddComponent<PlayerStartPoint>();

        var list = new List<Transform>();
        if (tutorialMode) return list;   // öğreticide combat yok — düşman marker'ı üretme

        // Düşman spawn marker'ları (batı + doğu zemin + orta) — kullanıcı düşmanı atar
        Vector3[] spots =
        {
            new Vector3( width * 0.5f - 3f, 1f,  5f),
            new Vector3( width * 0.5f - 3f, 1f, -5f),
            new Vector3(-width * 0.5f + 4f, 1f,  5f),
            new Vector3(-width * 0.5f + 4f, 1f, -5f),
            new Vector3( 0f, height * 0.55f, 0f),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var m = new GameObject($"SpawnPoint_{i}");
            m.transform.SetParent(parent.transform, false);
            m.transform.localPosition = spots[i];
            list.Add(m.transform);
        }
        return list;
    }

    // ───────── Yardımcı ─────────

    void MakeSlab(string name, Vector3 localPos, Vector3 size, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        go.isStatic = true;
    }
}
}
