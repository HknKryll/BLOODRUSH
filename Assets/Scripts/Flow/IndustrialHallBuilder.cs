using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Ch3 greybox kurucu: çok katlı, asimetrik sanayi/server salonu (ArenaBuilder'ın
// dairesel arenasının yerine geçer — bkz. plan: dairesel yapı merkeze daraldığı
// için terk edildi). Boş bir GO'ya ekle, ⋮ menüsünden "Salonu Kur" seç.
//
// Üretilenler:
// - Dikdörtgen kabuk: zemin + tavan + 4 duvar; batı duvarında giriş, doğu
//   duvarında boss odasına açılan çıkış boşluğu (üstleri lentolu).
// - platforms[] listesindeki her kayıt için: platform döşemesi + köşe kolonları
//   + (istenirse) StairBuilder desenli tam-dolu basamaklı merdiven (NavMesh
//   katları bağlayabilsin diye basamaklar zeminden doludur) + (istenirse)
//   ElevatedSpawnPoint / TerminalSpot marker'ı.
// - 4 zemin SpawnPoint marker'ı, zemin TerminalSpot marker'ları, PlayerStart
//   (PlayerStartPoint component'li), server rafı siperleri, emissive tavan
//   panelleri + panel başına gerçek ışık.
//
// Varsayılan yerleşim kasıtlı olarak asimetrik. Tüm çıktı normal sahne objesi —
// kurduktan sonra istediğin parçayı elle taşı/sil/çoğalt; "Salonu Kur" tekrar
// çalıştırılırsa eski çıktıyı silip parametrelerden yeniden üretir.
namespace Bloodrush.Flow
{
public class IndustrialHallBuilder : MonoBehaviour
{
    [Serializable]
    public class Platform
    {
        public string  name          = "Platform";
        [Tooltip("Yerel konum. Y = platformun ÜST yüzeyinin yüksekliği.")]
        public Vector3 center        = new Vector3(0f, 4f, 0f);
        [Tooltip("X-Z boyut (metre).")]
        public Vector2 size          = new Vector2(10f, 8f);
        [Tooltip("Bu platforma çıkan merdiven kurulsun mu?")]
        public bool    buildStair    = true;
        [Tooltip("Merdivenin platform kenarından DIŞA indiği yön (derece, 0=+Z). Merdiven her zaman ZEMİNE kadar iner, basamaklar zemine kadar doludur — havada kalamaz.")]
        public float   stairYawDeg   = 180f;
        [Tooltip("Üzerine ElevatedSpawnPoint marker'ı konsun mu? (WaveDirector'ın menzilli spawn'ları)")]
        public bool    spawnMarker   = false;
        [Tooltip("Üzerine TerminalSpot marker'ı konsun mu? (DataTerminal buraya oturtulur)")]
        public bool    terminalMarker = false;
    }

    [Serializable]
    public class Rack
    {
        public Vector3 position = Vector3.zero;   // yerel, y=0 taban
        public float   yawDeg   = 0f;
    }

    [Header("Salon Kabuğu (metre)")]
    [SerializeField] float width  = 46f;   // X
    [SerializeField] float depth  = 34f;   // Z
    [SerializeField] float height = 12f;   // Y
    [SerializeField] float wallThickness = 0.5f;

    [Header("Yerleşim Ölçeği")]
    [Tooltip("İç yerleşimi (platform/raf/marker konumları + platform boyutları) " +
             "orantılı büyütür. Kabuğu AYRICA width/depth ile büyüt. Haritayı " +
             "genişletmek için: width/depth'i artır (örn. 64/48) + bunu ~1.35 yap.")]
    [SerializeField] float layoutScale = 1f;

    [Header("Kapı Boşlukları")]
    [SerializeField] float entryGapWidth = 6f;    // batı duvarı (oyuncu girişi)
    [SerializeField] float exitGapWidth  = 6f;    // doğu duvarı (boss odasına)
    [SerializeField] float doorHeight    = 4.5f;  // boşluk yüksekliği (üstü lento)

    [Header("Katlar / Platformlar (asimetrik varsayılan — elle düzenle)")]
    [SerializeField] Platform[] platforms =
    {
        // Orta kat (~4m) — üç platform, farklı boy/konum
        new Platform { name = "OrtaPlatform_Bati",  center = new Vector3(-14f, 4f,  8f), size = new Vector2(10f, 8f),
                       stairYawDeg = 180f, terminalMarker = true },
        new Platform { name = "OrtaPlatform_Guney", center = new Vector3(  6f, 4f, -9f), size = new Vector2(12f, 7f),
                       stairYawDeg =  90f, spawnMarker = true },
        new Platform { name = "OrtaPlatform_Dogu",  center = new Vector3( 16f, 4f, 10f), size = new Vector2( 8f, 6f),
                       stairYawDeg = 270f },
        // Üst köprüler (~8m) — merdivenleri uzun ama zemine kadar iner (dolu bloklar)
        // Not: center.x=4 kasıtlı — bunun kolonları eskiden OrtaPlatform_Bati'nin
        // merdivenine giriyordu (iki AYRI platform arasındaki çakışma, kendi
        // merdiveniyle kendi kolonu çakışmasından farklı bir durum).
        new Platform { name = "Kopru_Merkez", center = new Vector3(4f, 8f,  2f), size = new Vector2(26f, 3f),
                       stairYawDeg =   0f, spawnMarker = true, terminalMarker = true },
        new Platform { name = "Kopru_Bati",   center = new Vector3(-16f, 8f, -6f), size = new Vector2(3f, 14f),
                       stairYawDeg =  90f, spawnMarker = true },
    };

    [Header("Merdiven")]
    [SerializeField] float stepHeight = 0.35f;
    [SerializeField] float stepDepth  = 0.5f;
    [SerializeField] float stairWidth = 3f;

    [Header("Terminaller")]
    [Tooltip("TerminalSpot'lara çalışır DataTerminal (kaide + dolum barı + trigger) otomatik kurulsun mu? Kapalıysa sadece boş marker konur, elle yerleştirirsin.")]
    [SerializeField] bool buildTerminals = true;

    [Header("Zemin Marker'ları")]
    [SerializeField] Vector3[] groundSpawnPoints =
    {
        new Vector3(-20f, 0.5f,  13f),
        new Vector3(-20f, 0.5f, -13f),
        new Vector3( 20f, 0.5f, -13f),
        new Vector3( 20f, 0.5f,  13f),
    };
    [SerializeField] Vector3[] groundTerminalSpots =
    {
        new Vector3(  -4f, 0f,  13f),
        new Vector3(  12f, 0f,  -3f),
        new Vector3( -15f, 0f, -11f),   // ek terminal (daha uzun bölüm için)
        new Vector3(  17f, 0f,  11f),   // ek terminal
    };
    [SerializeField] Vector3 playerStartPos = new Vector3(-20f, 1.2f, 0f);   // batı girişin içi

    [Header("Server Rafları (siper)")]
    [SerializeField] bool buildRacks = true;
    [SerializeField] Vector3 rackSize = new Vector3(3f, 2f, 1.2f);
    [SerializeField] Rack[] racks =
    {
        new Rack { position = new Vector3( -8f, 0f,   3f), yawDeg =  0f },
        new Rack { position = new Vector3( -3f, 0f,  -2f), yawDeg = 45f },
        new Rack { position = new Vector3(  2f, 0f,   8f), yawDeg = 30f },
        new Rack { position = new Vector3(  8f, 0f,   2f), yawDeg = 90f },
        new Rack { position = new Vector3( 16f, 0f, -14f), yawDeg =  0f },
        new Rack { position = new Vector3( -1f, 0f, -13f), yawDeg =  0f },
        new Rack { position = new Vector3(-13f, 0f,  -5f), yawDeg = 60f },
        new Rack { position = new Vector3( 18f, 0f,   4f), yawDeg =  0f },
    };

    [Header("Tavan Panelleri (emissive floresan)")]
    [SerializeField] bool  buildPanels     = true;
    [SerializeField] int   panelCols       = 4;     // X yönünde
    [SerializeField] int   panelRows       = 3;     // Z yönünde
    [SerializeField] Vector2 panelSize      = new Vector2(3f, 1.5f);
    [SerializeField] Color panelColor      = new Color(0.85f, 0.92f, 1f);   // soğuk floresan
    [SerializeField] float panelGlow       = 2f;
    [SerializeField] float panelLightLumen = 3500f;   // 0 = panel altı gerçek ışık kapalı

    [ContextMenu("Salonu Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        BuildShell();

        foreach (var p in platforms)
            BuildPlatform(ScaledPlatform(p));

        BuildMarkers();
        if (buildRacks)  BuildRackBlocks();
        if (buildPanels) BuildCeilingPanels();
    }

    // ───────────────── Kabuk ─────────────────

    void BuildShell()
    {
        float t = wallThickness;

        MakeSlab("Zemin", new Vector3(0f, -t * 0.5f, 0f),          new Vector3(width + t * 2f, t, depth + t * 2f));
        MakeSlab("Tavan", new Vector3(0f, height + t * 0.5f, 0f),  new Vector3(width + t * 2f, t, depth + t * 2f));

        // Kuzey/Güney duvarları düz
        MakeSlab("Duvar_Kuzey", new Vector3(0f, height * 0.5f,  depth * 0.5f + t * 0.5f), new Vector3(width + t * 2f, height, t));
        MakeSlab("Duvar_Guney", new Vector3(0f, height * 0.5f, -depth * 0.5f - t * 0.5f), new Vector3(width + t * 2f, height, t));

        // Batı duvarı: giriş boşluklu; Doğu duvarı: boss çıkışı boşluklu
        BuildGappedWall("Duvar_Bati", -width * 0.5f - t * 0.5f, entryGapWidth);
        BuildGappedWall("Duvar_Dogu",  width * 0.5f + t * 0.5f, exitGapWidth);
    }

    // X sabit bir duvarı, Z ortasında kapı boşluğu bırakarak üç parça kurar:
    // iki yan kanat (tam yükseklik) + boşluğun üstünde lento.
    void BuildGappedWall(string name, float x, float gapWidth)
    {
        var parent = new GameObject(name);
        parent.transform.SetParent(transform, false);
        float t = wallThickness;
        float sideLen = (depth - gapWidth) * 0.5f;

        MakeSlab("Kanat_Kuzey", new Vector3(x, height * 0.5f,  (gapWidth + sideLen) * 0.5f), new Vector3(t, height, sideLen), parent.transform);
        MakeSlab("Kanat_Guney", new Vector3(x, height * 0.5f, -(gapWidth + sideLen) * 0.5f), new Vector3(t, height, sideLen), parent.transform);

        float lintelH = height - doorHeight;
        if (lintelH > 0.05f)
            MakeSlab("Lento", new Vector3(x, doorHeight + lintelH * 0.5f, 0f), new Vector3(t, lintelH, gapWidth), parent.transform);
    }

    // ───────────────── Platformlar ─────────────────

    void BuildPlatform(Platform p)
    {
        var parent = new GameObject(p.name);
        parent.transform.SetParent(transform, false);

        const float slabT = 0.4f;
        Vector3 top = p.center;

        MakeSlab("Doseme", new Vector3(top.x, top.y - slabT * 0.5f, top.z),
                 new Vector3(p.size.x, slabT, p.size.y), parent.transform);

        // Merdivenin çıktığı yön — bu kenara yakın köşe(ler)e kolon koyulmayacak
        // (merdiven geniş olduğu için köşe kolonuyla çakışıyordu)
        Vector3 stairDir = Vector3.zero;
        if (p.buildStair)
        {
            float sAng = p.stairYawDeg * Mathf.Deg2Rad;
            stairDir = new Vector3(Mathf.Sin(sAng), 0f, Mathf.Cos(sAng));
        }

        // Köşe kolonları — zeminden platform altına (endüstriyel görünüm + altından geçilir)
        float colH = top.y - slabT;
        if (colH > 0.5f)
        {
            for (int cx = -1; cx <= 1; cx += 2)
            for (int cz = -1; cz <= 1; cz += 2)
            {
                if (p.buildStair && Vector3.Dot(new Vector3(cx, 0f, cz).normalized, stairDir) > 0.3f)
                    continue;   // merdiven çıkışına yakın köşe — atla

                Vector3 pos = new Vector3(
                    top.x + cx * (p.size.x * 0.5f - 0.4f),
                    colH * 0.5f,
                    top.z + cz * (p.size.y * 0.5f - 0.4f));
                MakeSlab($"Kolon_{cx}_{cz}", pos, new Vector3(0.6f, colH, 0.6f), parent.transform);
            }
        }

        if (p.buildStair) BuildStair(p, parent.transform);

        if (p.spawnMarker)
            MakeMarker($"ElevatedSpawnPoint_{p.name}", top + Vector3.up * 0.5f, parent.transform);
        if (p.terminalMarker)
            MakeTerminalOrMarker(p.name, top, parent.transform);
    }

    // Platform kenarından stairYawDeg yönünde DIŞA, ZEMİNE kadar inen merdiven.
    // StairBuilder deseni: her basamak zeminden doludur — hiçbir basamak havada
    // kalamaz, NavMesh/oyuncu güvenle çıkar, alt kısım zeminde siper görevi görür.
    void BuildStair(Platform p, Transform parent)
    {
        var stairGO = new GameObject("Merdiven");
        stairGO.transform.SetParent(parent, false);

        float ang = p.stairYawDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));   // platformdan dışa

        // Kenar noktası: merkezden dir yönünde yarı-boyut kadar
        float halfExtent = Mathf.Abs(dir.x) * p.size.x * 0.5f + Mathf.Abs(dir.z) * p.size.y * 0.5f;
        Vector3 edge = new Vector3(p.center.x, 0f, p.center.z) + dir * halfExtent;

        float topY  = p.center.y;
        int   steps = Mathf.Max(1, Mathf.CeilToInt(topY / stepHeight));

        for (int i = 0; i < steps; i++)
        {
            float blockH = topY - (i + 1) * (topY / steps);   // zeminden basamak üstüne
            if (blockH <= 0.02f) continue;

            Vector3 pos = edge + dir * (stepDepth * (i + 0.5f));
            pos.y = blockH * 0.5f;

            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Basamak_{i}";
            step.transform.SetParent(stairGO.transform, false);
            step.transform.localPosition = pos;
            step.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            step.transform.localScale    = new Vector3(stairWidth, blockH, stepDepth + 0.05f);
            step.isStatic = true;
        }
    }

    // ───────────────── Marker / Raf / Panel ─────────────────

    void BuildMarkers()
    {
        var parent = new GameObject("Markerlar");
        parent.transform.SetParent(transform, false);

        for (int i = 0; i < groundSpawnPoints.Length; i++)
            MakeMarker($"SpawnPoint_{i}", SXZ(groundSpawnPoints[i]), parent.transform);

        for (int i = 0; i < groundTerminalSpots.Length; i++)
            MakeTerminalOrMarker($"Zemin_{i}", SXZ(groundTerminalSpots[i]), parent.transform);

        var ps = MakeMarker("PlayerStart", SXZ(playerStartPos), parent.transform);
        ps.transform.localRotation = Quaternion.LookRotation(Vector3.right);   // doğuya (salona) bak
        ps.AddComponent<PlayerStartPoint>();
    }

    void BuildRackBlocks()
    {
        var parent = new GameObject("ServerRaflari");
        parent.transform.SetParent(transform, false);

        for (int i = 0; i < racks.Length; i++)
        {
            var r = racks[i];
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"Raf_{i}";
            seg.transform.SetParent(parent.transform, false);
            seg.transform.localPosition = new Vector3(r.position.x * layoutScale, rackSize.y * 0.5f, r.position.z * layoutScale);
            seg.transform.localRotation = Quaternion.Euler(0f, r.yawDeg, 0f);
            seg.transform.localScale    = rackSize;
            seg.isStatic = true;
        }
    }

    // ArenaBuilder'daki emissive panel tekniği, halka yerine dikdörtgen grid.
    // Panel görünümü HDRP/Unlit + HDR renk (Bloom yakalar), altına gerçek ışık.
    void BuildCeilingPanels()
    {
        var parent = new GameObject("TavanPanelleri");
        parent.transform.SetParent(transform, false);

        var mat = new Material(Shader.Find("HDRP/Unlit"));
        mat.SetColor("_UnlitColor", panelColor * panelGlow);

        float y = height - 0.25f;
        for (int cx = 0; cx < panelCols; cx++)
        for (int cz = 0; cz < panelRows; cz++)
        {
            Vector3 pos = new Vector3(
                -width * 0.5f + width * (cx + 0.5f) / panelCols,
                y,
                -depth * 0.5f + depth * (cz + 0.5f) / panelRows);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = $"Panel_{cx}_{cz}";
            panel.transform.SetParent(parent.transform, false);
            panel.transform.localPosition = pos;
            panel.transform.localScale    = new Vector3(panelSize.x, 0.15f, panelSize.y);
            DestroyImmediate(panel.GetComponent<Collider>());   // mermi/kanca takılmasın
            panel.GetComponent<Renderer>().sharedMaterial = mat;

            if (panelLightLumen > 0f)
            {
                var lgo = new GameObject($"PanelIsik_{cx}_{cz}");
                lgo.transform.SetParent(parent.transform, false);
                lgo.transform.localPosition = pos + Vector3.down * 0.6f;
                var light   = lgo.AddComponent<Light>();
                light.type  = LightType.Point;
                light.color = panelColor;
                light.range = height * 1.2f;
                var hd = lgo.AddComponent<HDAdditionalLightData>();
                hd.SetIntensity(panelLightLumen, LightUnit.Lumen);
                hd.EnableShadows(false);
            }
        }
    }

    // buildTerminals açıksa çalışır DataTerminal kurar, kapalıysa boş marker koyar.
    // Terminal: trigger hacim (oyuncu içinde durunca dolar) + "Glow" kaidesi
    // (idle gri / aktif sarı / bitti yeşil — DataTerminal isimle kendisi bulur)
    // + "FillBar" ilerleme çubuğu. WaveDirector/GameHUD bağlantısı otomatik
    // (DataTerminal singleton/statik üzerinden haber verir, elle bağlantı gerekmez).
    void MakeTerminalOrMarker(string suffix, Vector3 localPos, Transform parent)
    {
        if (!buildTerminals)
        {
            MakeMarker($"TerminalSpot_{suffix}", localPos, parent);
            return;
        }

        var go = new GameObject($"DataTerminal_{suffix}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var trigger = go.AddComponent<BoxCollider>();   // DataTerminal.Start isTrigger yapar
        trigger.center = new Vector3(0f, 1.2f, 0f);
        trigger.size   = new Vector3(3.5f, 2.5f, 3.5f);

        var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glow.name = "Glow";
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        glow.transform.localScale    = new Vector3(0.8f, 1.2f, 0.8f);

        var fillBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillBar.name = "FillBar";
        fillBar.transform.SetParent(go.transform, false);
        fillBar.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        fillBar.transform.localScale    = new Vector3(1.4f, 0.15f, 0.15f);
        DestroyImmediate(fillBar.GetComponent<Collider>());   // bara mermi/kanca takılmasın

        go.AddComponent<DataTerminal>();
    }

    // ───────────────── Yardımcılar ─────────────────

    // İç yerleşimi orantılı büyütür: XZ'yi layoutScale ile çarpar, Y (yükseklik) sabit.
    Vector3 SXZ(Vector3 v) => new Vector3(v.x * layoutScale, v.y, v.z * layoutScale);

    // Platform'un XZ konumunu ve boyutunu ölçekler; Y/merdiven ayarları aynen kopyalanır.
    Platform ScaledPlatform(Platform p) => new Platform
    {
        name           = p.name,
        center         = SXZ(p.center),
        size           = p.size * layoutScale,
        buildStair     = p.buildStair,
        stairYawDeg    = p.stairYawDeg,
        spawnMarker    = p.spawnMarker,
        terminalMarker = p.terminalMarker,
    };

    GameObject MakeMarker(string name, Vector3 localPos, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go;
    }

    void MakeSlab(string name, Vector3 localPos, Vector3 size, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        go.isStatic = true;   // NavMesh bake için
    }
}
}
