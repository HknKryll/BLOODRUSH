using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Flip Kulesi greybox'ı (IndustrialHall/GravityRoom deseni). Boş bir GameObject'e ekle →
// ⋮ menüsünden "Flip Kulesi Kur". Oyuncu ZIPLA=FLIP ile zemin↔tavan platformları
// arasında YUKARI tırmanır, tepedeki çıkışa ulaşır. Traversal odaklı.
//
// DÜZEN — "yürüyen tırmanış" (marching): platformlar batıdan doğuya ilerlerken tırmanır,
// her platform BENZERSİZ bir X'te. (Zikzak/2-kolon denendi ama aynı kolonda üst üste
// gelen zeminler, alttakinin yukarı-flip'ini BLOKLUYOR — marching'de bu imkânsız.)
//
// Choreography:
//  • ZEMİN (üstü yukarı, normal basılır) ile TAVAN (üstü aşağı, flip'te altına basılır)
//    sırayla. Ardışık iki zemin arasında bir tavan bulunur (ikisini de kapsar).
//  • Yukarı-flip ÇOK yükseltir (+Hup); aşağı-flip AZ indirir (−Hdown) → her zemin bir
//    öncekinden net +(Hup−Hdown) yukarıda. Hup > Hdown olmalı.
//
// Akış: batı-dip ZEMİN'de başla → doğuya, üstteki TAVAN'ın altına yürü → flip yukarı →
// tavana yüksel → doğuya yürü → flip aşağı → bir sonraki ZEMİN'e net yüksel → tekrarla →
// en doğu-tepe ZEMİN = çıkış (doğu duvarındaki boşluk).
namespace Bloodrush.Flow
{
public class GravityTowerBuilder : MonoBehaviour
{
    [Header("Şaft (metre)")]
    [SerializeField] float shaftWidth   = 30f;   // X (batı→doğu tırmanış ekseni)
    [SerializeField] float shaftDepth   = 10f;   // Z
    [SerializeField] float wallThickness = 1f;
    [SerializeField] float topY         = 22f;   // tepe kapağının altı
    [SerializeField] float bottomY      = -4f;   // dip zemin (başlangıç zemininin altında düşme çukuru)

    [Header("Tırmanış")]
    [Tooltip("Zemin platform sayısı. Tavanlar aralarına gelir (floorCount-1 tane).")]
    [SerializeField] int   floorCount = 5;
    [Tooltip("Yukarı-flip kazancı (tavan, alttaki zeminden bu kadar yukarıda).")]
    [SerializeField] float Hup   = 7f;
    [Tooltip("Aşağı-flip kaybı. Hup'tan KÜÇÜK olmalı → net tırmanış = Hup−Hdown.")]
    [SerializeField] float Hdown = 3f;
    [SerializeField] float floorWidth = 4f;    // X — dar tut ki flip yolları temiz kalsın
    [SerializeField] float platDepth  = 6f;    // Z
    [SerializeField] float platThickness = 0.5f;

    [Header("Çıkış (doğu duvarı, en üst zemin hizası)")]
    [SerializeField] float exitGapWidth = 4f;
    [SerializeField] float doorHeight   = 4f;

    Material emissiveMat;

    [ContextMenu("Flip Kulesi Kur")]
    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        emissiveMat = new Material(Shader.Find("HDRP/Unlit"));
        emissiveMat.SetColor("_UnlitColor", new Color(0.55f, 0.7f, 1f) * 2f);

        float topFloorS = BuildClimbPlatforms(out float topFloorX);
        float startX    = -(shaftWidth * 0.5f - 3f);   // P0 (başlangıç zemini) X'i
        BuildShaft(topFloorS);
        BuildEntry(startX);                            // batı giriş köprüsü + bariyer
        BuildExitLedge(topFloorS, topFloorX);
        BuildFlipZone();
        BuildFallCatcher();
        BuildBottomHazard();                           // dipte diken + parlayan enerji zemini (ölüm görseli)
        BuildShaftLights();
    }

    // Yürüyen zemin/tavan dizisi. out: en üst zemin X'i. Dönüş: en üst zemin yüzey Y'si.
    float BuildClimbPlatforms(out float topFloorX)
    {
        var parent = new GameObject("Platformlar");
        parent.transform.SetParent(transform, false);

        int   fc   = Mathf.Max(2, floorCount);
        float maxX = shaftWidth * 0.5f - 3f;            // uçlar duvara ~3m
        float net  = Hup - Hdown;                        // zemin başına net tırmanış
        Vector3 floorSize = new Vector3(floorWidth, platThickness, platDepth);

        float prevX = 0f, prevS = 0f;
        topFloorX = -maxX;
        float topFloorS = 0f;

        for (int fi = 0; fi < fc; fi++)
        {
            float fx = fc > 1 ? Mathf.Lerp(-maxX, maxX, (float)fi / (fc - 1)) : 0f;
            float fs = fi == 0 ? 0f : prevS + net;

            // ZEMİN: duruş-yüzeyi = üst yüz → merkez yüzeyin altında
            var floor = MakeSlab($"Zemin_{fi}", new Vector3(fx, fs - platThickness * 0.5f, 0f), floorSize, parent.transform);
            AddCheckpoint(fx, fs);
            if (fi == 0) BuildPlayerStart(new Vector3(fx, fs, 0f));

            // Önceki zemin ile bu zemin arasına TAVAN (öncekinin Hup üstünde, ikisini kapsar)
            if (fi > 0)
            {
                float cx = (prevX + fx) * 0.5f;
                float cs = prevS + Hup;                          // tavan alt yüzü
                float cw = Mathf.Abs(fx - prevX) + floorWidth;   // iki zemini de kapsa
                MakeSlab($"Tavan_{fi}", new Vector3(cx, cs + platThickness * 0.5f, 0f),
                         new Vector3(cw, platThickness, platDepth), parent.transform);
            }

            prevX = fx; prevS = fs;
            topFloorX = fx; topFloorS = fs;
        }
        return topFloorS;
    }

    // Her zemine standart Checkpoint (proje sistemi: düşme/ölüm → son checkpoint'te canlan)
    void AddCheckpoint(float x, float surfaceY)
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(x, surfaceY + 1f, 0f);   // KÖKE göre (kule taşınınca kopmasın)
        go.transform.localRotation = Quaternion.LookRotation(Vector3.right);   // respawn'da doğuya bak
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(floorWidth + 1f, 2f, platDepth);
        go.AddComponent<Checkpoint>();
    }

    void BuildPlayerStart(Vector3 surfacePos)
    {
        var ps = new GameObject("PlayerStart");
        ps.transform.SetParent(transform, false);
        ps.transform.localPosition = surfacePos + Vector3.up * 1.1f;      // KÖKE göre
        ps.transform.localRotation = Quaternion.LookRotation(Vector3.right);   // doğuya bak (tırmanış yönü)
        ps.AddComponent<PlayerStartPoint>();
    }

    // En üst zeminden (doğu) duvara köprü + çıkış boşluğu
    void BuildExitLedge(float topFloorS, float topFloorX)
    {
        float t  = wallThickness;
        float hw = shaftWidth * 0.5f;

        // En üst zeminden doğu duvarına ince ledge (çıkış şeridinde, tırmanışın DOĞUsunda → hiçbir flip'i bloklamaz)
        float ledgeCx = (topFloorX + hw) * 0.5f;
        float ledgeW  = hw - topFloorX + 0.5f;
        if (ledgeW > 0.2f)
            MakeSlab("CikisLedge", new Vector3(ledgeCx, topFloorS - platThickness * 0.5f, 0f),
                     new Vector3(ledgeW, platThickness, exitGapWidth + 1f));

        // Çıkışı kapatan bariyer (kullanıcı istediği koşulda açar/kaldırır)
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "ExitBarrier";
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = new Vector3(hw + t * 0.5f, topFloorS + doorHeight * 0.5f, 0f);
        bar.transform.localScale    = new Vector3(t, doorHeight, exitGapWidth);
    }

    // Şaft kabuğu: dip + tepe kapağı + kuzey/güney duvar + batı (giriş boşluklu) + doğu (çıkış boşluklu)
    void BuildShaft(float exitFloorS)
    {
        var parent = new GameObject("Saft");
        parent.transform.SetParent(transform, false);
        float t  = wallThickness;
        float hw = shaftWidth * 0.5f;
        float hd = shaftDepth * 0.5f;
        float midY  = (bottomY + topY) * 0.5f;
        float fullH = topY - bottomY;

        MakeSlab("DipZemin",  new Vector3(0f, bottomY - t * 0.5f, 0f), new Vector3(shaftWidth + t * 2f, t, shaftDepth + t * 2f), parent.transform);
        MakeSlab("TepeKapak", new Vector3(0f, topY + t * 0.5f, 0f),    new Vector3(shaftWidth + t * 2f, t, shaftDepth + t * 2f), parent.transform);
        MakeSlab("Duvar_Kuzey", new Vector3(0f, midY,  hd + t * 0.5f), new Vector3(shaftWidth + t * 2f, fullH, t), parent.transform);
        MakeSlab("Duvar_Guney", new Vector3(0f, midY, -hd - t * 0.5f), new Vector3(shaftWidth + t * 2f, fullH, t), parent.transform);

        BuildGappedWall(parent.transform, "Duvar_Bati", -hw - t * 0.5f, 0f);          // giriş = başlangıç zemini (S=0)
        BuildGappedWall(parent.transform, "Duvar_Dogu",  hw + t * 0.5f, exitFloorS);  // çıkış  = en üst zemin
    }

    // Bir yan duvar, belirtilen zemin hizasında (floorS) yatay bir boşlukla (alt/üst/yan parçalar)
    void BuildGappedWall(Transform parent, string prefix, float x, float floorS)
    {
        float t      = wallThickness;
        float gapBot = floorS;
        float gapTop = floorS + doorHeight;
        float sideLen = (shaftDepth - exitGapWidth) * 0.5f;

        float belowH = gapBot - bottomY;
        if (belowH > 0.05f)
            MakeSlab($"{prefix}_Alt", new Vector3(x, bottomY + belowH * 0.5f, 0f), new Vector3(t, belowH, shaftDepth), parent);
        float aboveH = topY - gapTop;
        if (aboveH > 0.05f)
            MakeSlab($"{prefix}_Ust", new Vector3(x, gapTop + aboveH * 0.5f, 0f), new Vector3(t, aboveH, shaftDepth), parent);
        if (sideLen > 0.05f)
        {
            MakeSlab($"{prefix}_YanK", new Vector3(x, (gapBot + gapTop) * 0.5f,  (exitGapWidth + sideLen) * 0.5f), new Vector3(t, doorHeight, sideLen), parent);
            MakeSlab($"{prefix}_YanG", new Vector3(x, (gapBot + gapTop) * 0.5f, -(exitGapWidth + sideLen) * 0.5f), new Vector3(t, doorHeight, sideLen), parent);
        }
    }

    // Batı giriş: başlangıç zemininden (S=0) batı duvarına köprü + kapatan EntryBarrier
    void BuildEntry(float startX)
    {
        float t  = wallThickness;
        float hw = shaftWidth * 0.5f;

        float ledgeCx = (startX + (-hw)) * 0.5f;
        float ledgeW  = startX - (-hw) + 0.5f;
        if (ledgeW > 0.2f)
            MakeSlab("GirisKopru", new Vector3(ledgeCx, -platThickness * 0.5f, 0f),
                     new Vector3(ledgeW, platThickness, exitGapWidth + 1f));

        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "EntryBarrier";
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = new Vector3(-hw - t * 0.5f, doorHeight * 0.5f, 0f);
        bar.transform.localScale    = new Vector3(t, doorHeight, exitGapWidth);
    }

    // Tüm şaftı kaplayan flip bölgesi (zıplayınca yerçekimi ters döner)
    void BuildFlipZone()
    {
        var go = new GameObject("FlipBolgesi");
        go.transform.SetParent(transform, false);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, (bottomY + topY) * 0.5f, 0f);
        box.size   = new Vector3(shaftWidth, topY - bottomY, shaftDepth);
        go.AddComponent<GravityFlipZone>();
    }

    // Dipte KillVolume — düşen oyuncuyu son checkpoint'te canlandırır (proje sistemi)
    void BuildFallCatcher()
    {
        var go = new GameObject("KillVolume");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, bottomY + 1f, 0f);   // KÖKE göre
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(shaftWidth, 1.5f, shaftDepth);
        go.AddComponent<KillVolume>();
    }

    // Işıklar: şaft GENİŞ (X) olduğu için hem Y (kat) hem X (uç→uç) boyunca dağıt — yoksa
    // uçlardaki platformlar karanlık kalıyor. Ortada point ışık (aydınlatır) + kuzey/güney
    // duvarda emissive panel (görsel).
    void BuildShaftLights()
    {
        var parent = new GameObject("SaftIsiklari");
        parent.transform.SetParent(transform, false);
        var col = new Color(0.62f, 0.76f, 1f);
        float hw = shaftWidth * 0.5f;
        float hd = shaftDepth * 0.5f;

        int rows = Mathf.Max(3, floorCount);   // Y katları
        int cols = 3;                          // X: batı ucu, orta, doğu ucu
        for (int r = 0; r < rows; r++)
        {
            float y = Mathf.Lerp(bottomY + 3f, topY - 2f, (float)r / (rows - 1));
            for (int c = 0; c < cols; c++)
            {
                float x = cols > 1 ? Mathf.Lerp(-hw + 3f, hw - 3f, (float)c / (cols - 1)) : 0f;

                // Aydınlatan point ışık (şaft merkezinde)
                var lgo = new GameObject($"Isik_{r}_{c}");
                lgo.transform.SetParent(parent.transform, false);
                lgo.transform.localPosition = new Vector3(x, y, 0f);
                var light   = lgo.AddComponent<Light>();
                light.type  = LightType.Point;
                light.color = col;
                light.range = 22f;
                var hdl = lgo.AddComponent<HDAdditionalLightData>();
                hdl.SetIntensity(3800f, LightUnit.Lumen);
                hdl.EnableShadows(false);

                // Görsel emissive paneller (kuzey + güney duvar)
                for (int s = -1; s <= 1; s += 2)
                {
                    var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panel.name = $"Panel_{r}_{c}_{s}";
                    panel.transform.SetParent(parent.transform, false);
                    panel.transform.localPosition = new Vector3(x, y, s * (hd - 0.15f));
                    panel.transform.localScale    = new Vector3(2.5f, 1.1f, 0.12f);
                    DestroyImmediate(panel.GetComponent<Collider>());
                    panel.GetComponent<Renderer>().sharedMaterial = emissiveMat;
                }
            }
        }
    }

    // Dip tehlikesi: kırmızı parlayan enerji zemini (bloom — "neden öldüm" belli) + diken
    // tarlası (siluet). Hepsi görsel; öldürme işini KillVolume yapar (collider yok).
    void BuildBottomHazard()
    {
        var parent = new GameObject("DipTehlike");
        parent.transform.SetParent(transform, false);

        // Parlayan enerji zemini (dip zeminin hemen üstünde)
        var glowMat = new Material(Shader.Find("HDRP/Unlit"));
        glowMat.SetColor("_UnlitColor", new Color(1f, 0.12f, 0.06f) * 3f);
        var glow = MakeSlab("EnerjiZemin", new Vector3(0f, bottomY + 0.06f, 0f),
                            new Vector3(shaftWidth - 0.5f, 0.1f, shaftDepth - 0.5f), parent.transform);
        DestroyImmediate(glow.GetComponent<Collider>());
        glow.GetComponent<Renderer>().sharedMaterial = glowMat;

        // Diken tarlası
        var spikeMat = new Material(Shader.Find("HDRP/Unlit"));
        spikeMat.SetColor("_UnlitColor", new Color(0.85f, 0.14f, 0.08f));
        var mesh = SpikeMesh();
        int nx = Mathf.Max(3, Mathf.RoundToInt(shaftWidth / 3f));
        int nz = Mathf.Max(2, Mathf.RoundToInt(shaftDepth / 3f));
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float px = Mathf.Lerp(-shaftWidth * 0.5f + 1.5f, shaftWidth * 0.5f - 1.5f, nx > 1 ? (float)ix / (nx - 1) : 0.5f);
            float pz = Mathf.Lerp(-shaftDepth * 0.5f + 1.5f, shaftDepth * 0.5f - 1.5f, nz > 1 ? (float)iz / (nz - 1) : 0.5f);
            float h  = Random.Range(1.2f, 1.9f);

            var sp = new GameObject($"Diken_{ix}_{iz}");
            sp.transform.SetParent(parent.transform, false);
            sp.transform.localPosition = new Vector3(px, bottomY + 0.05f, pz);
            sp.transform.localScale    = new Vector3(0.9f, h, 0.9f);
            sp.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            sp.AddComponent<MeshFilter>().sharedMesh     = mesh;
            sp.AddComponent<MeshRenderer>().sharedMaterial = spikeMat;
            sp.isStatic = true;
        }
    }

    static Mesh _spikeMesh;
    // 4 yüzlü piramit (diken). Yan üçgenler ÇİFT yönlü → culling'den bağımsız her açıdan görünür.
    static Mesh SpikeMesh()
    {
        if (_spikeMesh != null) return _spikeMesh;
        var m = new Mesh { name = "Diken" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f), new Vector3(-0.5f, 0f, 0.5f),
            new Vector3( 0f,   1f,  0f)   // tepe
        };
        m.triangles = new[]
        {
            0,1,4, 4,1,0,   1,2,4, 4,2,1,
            2,3,4, 4,3,2,   3,0,4, 4,0,3,
            0,2,1, 0,3,2    // taban
        };
        m.RecalculateNormals();
        m.RecalculateBounds();
        _spikeMesh = m;
        return m;
    }

    GameObject MakeSlab(string name, Vector3 localPos, Vector3 size, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        go.isStatic = true;
        return go;
    }
}
}
