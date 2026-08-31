using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Sıralı güvenlik kilidi odasını greybox olarak üretir (IndustrialHallBuilder ile
// aynı desen). Boş bir GO'ya ekle → ⋮ "Puzzle Odası Kur". Üretir:
// - Kabuk: zemin/tavan/4 duvar (batı girişi + doğu boss çıkışı boşluklu).
// - entryBarrier (giriş boşluğunu kapatan blok) + bossDoorBarrier (çıkış boşluğu).
// - N konsol (PuzzleConsole + renkli Glow + aktivasyon trigger'ı) + konsol başına
//   spawn marker'ları.
// - Merkezi sıra ekranı (N renk slotu).
// - Ceza spawn marker'ları, PlayerStart, oda-hacmi trigger'lı SequenceLock.
// Hepsi SequenceLock/PuzzleConsole'a Configure ile otomatik bağlanır; kullanıcı
// sonra her konsola düşman prefabı+sayısı ve ceza prefabını atar.
namespace Bloodrush.Flow
{
public class PuzzleRoomBuilder : MonoBehaviour
{
    [Header("Oda (metre)")]
    [SerializeField] float width  = 26f;
    [SerializeField] float depth  = 20f;
    [SerializeField] float height = 8f;
    [SerializeField] float wallThickness = 0.5f;

    [Header("Kapı boşlukları")]
    [SerializeField] float entryGapWidth = 4f;   // batı: giriş
    [SerializeField] float exitGapWidth  = 4f;   // doğu: boss çıkışı
    [SerializeField] float doorHeight    = 4f;

    [Header("Konsollar (renk sayısı = konsol sayısı)")]
    [SerializeField] Color[] consoleColors =
    {
        new Color(1f, 0.3f, 0.3f),   // kırmızı
        new Color(0.3f, 1f, 0.4f),   // yeşil
        new Color(0.4f, 0.6f, 1f),   // mavi
    };
    [SerializeField] int spawnsPerConsole = 2;

    [Header("Ceza spawn")]
    [SerializeField] int penaltySpawnCount = 3;

    [Header("Tavan Işıkları")]
    [SerializeField] int     lightCols      = 3;
    [SerializeField] int     lightRows      = 2;
    [SerializeField] Color   lightColor     = new Color(0.85f, 0.92f, 1f);   // soğuk floresan
    [SerializeField] float   lightGlow      = 2f;
    [SerializeField] float   lightLumen     = 3000f;   // 0 = gerçek ışık kapalı
    [SerializeField] Vector2 lightPanelSize = new Vector2(3f, 1.5f);

    Material emissiveMat;   // paylaşımlı HDRP/Unlit (runtime her renderer kendi kopyasını renklendirir)

    [ContextMenu("Puzzle Odası Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        emissiveMat = new Material(Shader.Find("HDRP/Unlit"));
        emissiveMat.SetColor("_UnlitColor", Color.white);

        BuildShell();
        BuildCeilingLights();

        var entryBarrier = BuildBarrier("EntryBarrier", -width * 0.5f, entryGapWidth);
        var bossBarrier  = BuildBarrier("BossDoorBarrier", width * 0.5f, exitGapWidth);

        int count = Mathf.Max(1, consoleColors.Length);
        for (int i = 0; i < count; i++)
            BuildConsole(i, count);

        var slots        = BuildScreen(count);
        var penaltySpawns = BuildPenaltySpawns();
        BuildPlayerStart();

        // SequenceLock denetleyicisi + oda-hacmi trigger'ı (oyuncu girince arkadan mühür)
        var lockGO = new GameObject("SequenceLock");
        lockGO.transform.SetParent(transform, false);
        var trig = lockGO.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0f, height * 0.5f, 0f);
        trig.size   = new Vector3(width - wallThickness * 2f, height, depth - wallThickness * 2f);
        var seq = lockGO.AddComponent<SequenceLock>();
        seq.Configure(entryBarrier, bossBarrier, slots.ToArray(), penaltySpawns.ToArray());
    }

    // ───────────────── Kabuk ─────────────────

    void BuildShell()
    {
        float t = wallThickness;
        MakeSlab("Zemin", new Vector3(0f, -t * 0.5f, 0f),         new Vector3(width + t * 2f, t, depth + t * 2f));
        MakeSlab("Tavan", new Vector3(0f, height + t * 0.5f, 0f), new Vector3(width + t * 2f, t, depth + t * 2f));
        MakeSlab("Duvar_Kuzey", new Vector3(0f, height * 0.5f,  depth * 0.5f + t * 0.5f), new Vector3(width + t * 2f, height, t));
        MakeSlab("Duvar_Guney", new Vector3(0f, height * 0.5f, -depth * 0.5f - t * 0.5f), new Vector3(width + t * 2f, height, t));
        BuildGappedWall("Duvar_Bati", -width * 0.5f - t * 0.5f, entryGapWidth);
        BuildGappedWall("Duvar_Dogu",  width * 0.5f + t * 0.5f, exitGapWidth);
    }

    // Emissive tavan panelleri + panel başına gerçek HDRP point ışık (IndustrialHallBuilder deseni)
    void BuildCeilingLights()
    {
        var parent = new GameObject("TavanIsiklari");
        parent.transform.SetParent(transform, false);

        var mat = new Material(Shader.Find("HDRP/Unlit"));
        mat.SetColor("_UnlitColor", lightColor * lightGlow);

        float y = height - 0.25f;
        for (int cx = 0; cx < lightCols; cx++)
        for (int cz = 0; cz < lightRows; cz++)
        {
            Vector3 pos = new Vector3(
                -width * 0.5f + width * (cx + 0.5f) / lightCols, y,
                -depth * 0.5f + depth * (cz + 0.5f) / lightRows);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = $"Panel_{cx}_{cz}";
            panel.transform.SetParent(parent.transform, false);
            panel.transform.localPosition = pos;
            panel.transform.localScale    = new Vector3(lightPanelSize.x, 0.15f, lightPanelSize.y);
            DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial = mat;

            if (lightLumen > 0f)
            {
                var lgo = new GameObject($"Isik_{cx}_{cz}");
                lgo.transform.SetParent(parent.transform, false);
                lgo.transform.localPosition = pos + Vector3.down * 0.6f;
                var light   = lgo.AddComponent<Light>();
                light.type  = LightType.Point;
                light.color = lightColor;
                light.range = height * 1.4f;
                var hd = lgo.AddComponent<HDAdditionalLightData>();
                hd.SetIntensity(lightLumen, LightUnit.Lumen);
                hd.EnableShadows(false);
            }
        }
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

    // Kapı boşluğunu kapatan blok — SequenceLock runtime'da aç/kapat yapar
    GameObject BuildBarrier(string name, float x, float gapWidth)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(x, doorHeight * 0.5f, 0f);
        go.transform.localScale    = new Vector3(wallThickness, doorHeight, gapWidth);
        go.isStatic = false;   // aç/kapat edilecek
        return go;
    }

    // ───────────────── Konsol ─────────────────

    PuzzleConsole BuildConsole(int i, int count)
    {
        // Konsolları oda önünde (giriş tarafına bakar) bir yay boyunca yay
        float tSpread = count > 1 ? (float)i / (count - 1) : 0.5f;
        float x = Mathf.Lerp(-width * 0.32f, width * 0.32f, tSpread);
        float z = Mathf.Lerp(depth * 0.28f, depth * 0.05f, Mathf.Abs(tSpread - 0.5f) * 2f); // ortadaki biraz ileri
        Vector3 pos = new Vector3(x, 0f, z);

        var go = new GameObject($"PuzzleConsole_{i}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;

        // Aktivasyon trigger'ı (yaklaşınca prompt)
        var trig = go.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0f, 1f, 0f);
        trig.size   = new Vector3(3f, 2.5f, 3f);

        // Gövde
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        body.transform.localScale    = new Vector3(0.9f, 1.2f, 0.6f);

        // Glow (renkli emissive — PuzzleConsole "Glow" ismiyle bulur)
        var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glow.name = "Glow";
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.3f, 0.2f);
        glow.transform.localScale    = new Vector3(0.7f, 0.3f, 0.3f);
        DestroyImmediate(glow.GetComponent<Collider>());
        glow.GetComponent<Renderer>().sharedMaterial = emissiveMat;

        // Konsol spawn marker'ları (arkasına, duvara doğru)
        var spawns = new Transform[spawnsPerConsole];
        for (int s = 0; s < spawnsPerConsole; s++)
        {
            var m = new GameObject($"Spawn_{s}");
            m.transform.SetParent(go.transform, false);
            m.transform.localPosition = new Vector3((s - (spawnsPerConsole - 1) * 0.5f) * 2f, 0.5f, 3f);
            spawns[s] = m.transform;
        }

        var pc = go.AddComponent<PuzzleConsole>();
        pc.Configure(i, consoleColors[i % consoleColors.Length], spawns);
        return pc;
    }

    // ───────────────── Sıra ekranı ─────────────────

    List<Renderer> BuildScreen(int count)
    {
        var screen = new GameObject("SiraEkrani");
        screen.transform.SetParent(transform, false);
        screen.transform.localPosition = new Vector3(0f, 3.5f, depth * 0.5f - 0.4f); // kuzey duvarında

        // Arka pano
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Pano";
        panel.transform.SetParent(screen.transform, false);
        panel.transform.localScale = new Vector3(count * 1.4f + 0.6f, 1.6f, 0.15f);

        var slots = new List<Renderer>();
        for (int i = 0; i < count; i++)
        {
            var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.name = $"Slot_{i}";
            slot.transform.SetParent(screen.transform, false);
            slot.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * 1.4f, 0f, -0.15f);
            slot.transform.localScale    = new Vector3(1f, 1f, 0.15f);
            DestroyImmediate(slot.GetComponent<Collider>());
            slot.GetComponent<Renderer>().sharedMaterial = emissiveMat;
            slots.Add(slot.GetComponent<Renderer>());
        }
        return slots;
    }

    // ───────────────── Ceza spawn / PlayerStart ─────────────────

    List<Transform> BuildPenaltySpawns()
    {
        var parent = new GameObject("CezaSpawnlari");
        parent.transform.SetParent(transform, false);
        var list = new List<Transform>();
        for (int i = 0; i < penaltySpawnCount; i++)
        {
            var m = new GameObject($"CezaSpawn_{i}");
            m.transform.SetParent(parent.transform, false);
            float ang = (i / (float)penaltySpawnCount) * Mathf.PI * 2f;
            m.transform.localPosition = new Vector3(Mathf.Sin(ang) * width * 0.35f, 0.5f, Mathf.Cos(ang) * depth * 0.3f);
            list.Add(m.transform);
        }
        return list;
    }

    void BuildPlayerStart()
    {
        var ps = new GameObject("PlayerStart");
        ps.transform.SetParent(transform, false);
        ps.transform.localPosition = new Vector3(-width * 0.5f + 2f, 1.2f, 0f);   // girişin içi
        ps.transform.localRotation = Quaternion.LookRotation(Vector3.right);       // salona bak
        ps.AddComponent<PlayerStartPoint>();
    }

    // ───────────────── Yardımcı ─────────────────

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
