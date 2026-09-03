using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Kuzey cephe: güvenlik kapısı + tavana kadar cam paneller + şehir silüeti manzarası.
// İKİ AŞAMALI, sırayla çalıştır:
//
//  1) "Kapı Boşluğu Aç" — TEK SEFERLİK. targetWall'a (ör. Duvar_Kuzey) bak, onun gerçek
//     dünya boyutlarından kapı+cam ölçülerini hesaplar, o duvarı SİLİP yerine 3 solid parça
//     (lento + iki kenar) koyar — hepsi MainRoom'un child'ı olarak kalır, orijinal materyali
//     korur. Bu builder'ın kendi transform'unu da doğru cephe merkezine (kapı ortası, zemin
//     hizası, duvar yüzü) otomatik taşır — elle konumlandırmana gerek yok.
//     targetWall silindiği için tekrar çalıştırmak güvenli no-op'tur.
//
//  2) "Giriş Cephesi Kur" — EKLEMELİ, istediğin kadar tekrar çalıştırılabilir. SADECE
//     kendi "GirisCephesiProps" child'ını kurar/yeniler: kapı kanatları (ElevatorDoor ile,
//     yeniden kod yazılmadı — mevcut component reuse edildi), cam paneller (collider'lı,
//     transparent HDRP/Lit), küçük bir niş + görünmez durdurucu (düşme güvenliği), şehir
//     silüeti (düz renk backdrop + kutu binalar) ve girişi vurgulayan spot ışık.
//     MainRoom'a HİÇ DOKUNMAZ.
//
// Yerel eksen (bu obje 1. adımdan sonra): +Z = dışa (şehre) doğru, -Z = oda içine doğru,
// Y=0 = zemin hizası, X=0 = kapı ortası.
namespace Bloodrush.Flow
{
public class EntranceFacadeBuilder : MonoBehaviour
{
    const string FacadeContainer = "GirisCephesiProps";

    [Header("1) Hedef Duvar (böl, sonra boş kalır)")]
    [Tooltip("Bölünecek solid duvar (ör. Duvar_Kuzey). 'Kapı Boşluğu Aç' sonrası yok edilir.")]
    [SerializeField] Transform targetWall;

    [Tooltip("1. adımın hesapladığı cephe merkezi (dünya konumu) — 2. adım BUNU kullanır, " +
             "bu objenin o anki transform'unu DEĞİL. Obje yanlışlıkla sürüklenirse/taşınırsa " +
             "bile cephe hep doğru yerde kurulur. Elle değiştirme.")]
    [SerializeField] Vector3 anchorWorldPos;
    [SerializeField] bool    hasAnchor;   // serialize edilir — domain reload'da kaybolmasın

    [Header("Kapı / Cam Ölçüleri (1. adım otomatik doldurur)")]
    [SerializeField] float doorWidth     = 3.6f;
    [SerializeField] float doorHeight    = 2.4f;
    [Tooltip("Kapının HER iki yanındaki cam bölge genişliği.")]
    [SerializeField] float glassWidth    = 3.0f;
    [SerializeField] float wallHeight    = 6.0f;
    [SerializeField] float wallThickness = 0.5f;
    [Tooltip("1. adımda MainRoom'un Zemin materyalinden otomatik doldurulur (niş zemini için).")]
    [SerializeField] Material floorMatCache;

    [Header("Kapı Kanatları")]
    [SerializeField] float doorThickness  = 0.15f;
    [SerializeField] Color doorColor      = new Color(0.15f, 0.16f, 0.18f);
    [SerializeField] float doorMetallic   = 0.8f;
    [SerializeField] float doorSmoothness = 0.5f;

    [Header("Cam")]
    [Tooltip("Alpha (a) = saydamlık. ~0.3 hafif saydam.")]
    [SerializeField] Color glassColor      = new Color(0.75f, 0.82f, 0.88f, 0.3f);
    [SerializeField] float glassMetallic   = 0.1f;
    [SerializeField] float glassSmoothness = 0.88f;

    [Header("Niş / Güvenlik")]
    [Tooltip("Kapı+cam önünde ne kadar yürünebilir alan olsun (düşme riski olmadan).")]
    [SerializeField] float nicheDepth = 2f;

    [Header("Dış Manzara — Mesafe")]
    [Tooltip("Durdurucudan dış manzaranın başlangıcına kadar boşluk.")]
    [SerializeField] float skylineGap   = 2f;
    [Tooltip("Bahçe+bina derinliği (durdurucudan gökyüzüne kadar).")]
    [SerializeField] float skylineDepth = 26f;
    [SerializeField] Color skyColor     = new Color(0.55f, 0.72f, 0.88f);   // açık gündüz mavisi
    [SerializeField] int   seed         = 2026;

    [Header("Bahçe (ön planda, gerçek ağaç modeli için slot var)")]
    [SerializeField] Color groundColor = new Color(0.26f, 0.42f, 0.16f);   // çim yeşili
    [Tooltip("Kaç ağaç serpilsin.")]
    [SerializeField] int   treeCount   = 10;
    [Tooltip("Bahçenin bina yönünde ne kadar derine yayılacağı (skylineDepth içinde, ön kısım).")]
    [SerializeField] float gardenDepth = 14f;
    [Tooltip("Gerçek ağaç modeli (Asset Store/Sketchfab) — atarsan greybox yerine bu kullanılır.")]
    [SerializeField] GameObject treePrefab;
    [SerializeField] float treeScale       = 1f;
    [SerializeField] Color treeTrunkColor  = new Color(0.32f, 0.22f, 0.14f);
    [SerializeField] Color treeLeafColor   = new Color(0.18f, 0.38f, 0.14f);

    [Header("Büyük Bina (bahçenin ötesinde, tek ve dominant)")]
    [SerializeField] float buildingWidth  = 16f;
    [SerializeField] float buildingDepth  = 12f;
    [SerializeField] float buildingHeight = 34f;
    [SerializeField] Color buildingColor  = new Color(0.62f, 0.60f, 0.56f);   // açık taş/beton tonu
    [Tooltip("Bina cephesine pencere ızgarası ekler.")]
    [SerializeField] bool  addBuildingWindows = true;
    [SerializeField] Color windowColor        = new Color(0.35f, 0.45f, 0.55f);   // mat cam tonu (gündüz — parlamaz)

    [Header("Spot Işık (girişi vurgular)")]
    [SerializeField] Color spotColor  = new Color(1f, 0.92f, 0.80f);
    [SerializeField] float spotLumen  = 4500f;
    [SerializeField] float spotRange  = 14f;
    [SerializeField] float spotAngle  = 70f;

    Material doorMat, glassMat, skyMat, windowMat;

    // ───────────────── 1) Duvarı böl (tek seferlik) ─────────────────

    [ContextMenu("1) Kapı Boşluğu Aç (Hedef Duvarı Böler)")]
    void SplitWall()
    {
        if (targetWall == null)
        {
            Debug.LogWarning("[EntranceFacadeBuilder] targetWall boş — zaten bölünmüş ya da hiç atanmamış.", this);
            return;
        }
        var rend = targetWall.GetComponent<Renderer>();
        if (rend == null) { Debug.LogError("[EntranceFacadeBuilder] targetWall'da Renderer yok.", this); return; }

#if UNITY_EDITOR
        // 3 parça oluşturma + eski duvarı silme TEK bir Ctrl+Z adımı olsun — ayrı ayrı
        // gruplanırsa tek geri alma sadece "silme"yi geri getirip parçaları bırakabilir
        // (eski duvar + yeni parçalar aynı anda var olur, üst üste biner).
        UnityEditor.Undo.SetCurrentGroupName("Kapı Boşluğu Aç");
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
#endif

        Bounds b = rend.bounds;                      // dünya AABB (duvar rotasyonsuz varsayılır)
        var mat = rend.sharedMaterial;
        Transform parent = targetWall.parent;

        float centerX  = (b.min.x + b.max.x) * 0.5f;
        float doorMinX = centerX - doorWidth * 0.5f;
        float doorMaxX = centerX + doorWidth * 0.5f;
        float glassWMinX = doorMinX - glassWidth;
        float glassEMaxX = doorMaxX + glassWidth;
        float floorY = b.min.y;
        float ceilY  = b.max.y;
        float doorTopY = floorY + doorHeight;
        float wallZ  = (b.min.z + b.max.z) * 0.5f;
        float wallThk = b.max.z - b.min.z;

        // Lento (kapının üstü, kapı genişliğinde, tam solid)
        MakeWallPiece(parent, "Duvar_Kuzey_Ust", mat, doorMinX, doorMaxX, doorTopY, ceilY, wallZ, wallThk);
        // Kenar duvarlar (cam bölgelerinin ötesi, tam yükseklik, orijinal görünüm)
        MakeWallPiece(parent, "Duvar_Kuzey_Bati", mat, b.min.x, glassWMinX, floorY, ceilY, wallZ, wallThk);
        MakeWallPiece(parent, "Duvar_Kuzey_Dogu", mat, glassEMaxX, b.max.x, floorY, ceilY, wallZ, wallThk);

        // Ölçüleri gerçek duvardan senkronla + niş zemini için MainRoom'un Zemin materyalini yakala
        doorWidth = doorMaxX - doorMinX;
        wallHeight = ceilY - floorY;
        wallThickness = wallThk;
        if (parent != null)
        {
            var floorChild = parent.Find("Zemin");
            if (floorChild != null && floorChild.TryGetComponent(out Renderer floorRend))
                floorMatCache = floorRend.sharedMaterial;
        }

        // Cephe merkezini (X=kapı ortası, Y=zemin, Z=duvar yüzü) AYRI bir alanda sakla —
        // "2) Giriş Cephesi Kur" bunu kullanır, bu objenin o anki transform'unu DEĞİL.
        // Obje sonradan yanlışlıkla sürüklenirse/taşınırsa bile cephe hep doğru yerde kurulur.
        anchorWorldPos = new Vector3(centerX, floorY, wallZ);
        hasAnchor = true;

        // Kolaylık olsun diye objeyi de aynı noktaya taşı (Scene view'da elle konumlandırma
        // gerekmesin) — ama 2. adım buna değil, yukarıdaki anchorWorldPos'a güvenir.
        transform.position = anchorWorldPos;
        transform.rotation = Quaternion.identity;

#if UNITY_EDITOR
        UnityEditor.Undo.DestroyObjectImmediate(targetWall.gameObject);
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);   // hepsi TEK Ctrl+Z adımı
#else
        DestroyImmediate(targetWall.gameObject);
#endif
        targetWall = null;

        Debug.Log($"[EntranceFacadeBuilder] Duvar bölündü. Kapı genişliği {doorWidth:F2}, " +
                  $"duvar yüksekliği {wallHeight:F2}. Builder cephe merkezine taşındı. " +
                  "Şimdi ⋮ '2) Giriş Cephesi Kur' çalıştır.", this);
    }

    GameObject MakeWallPiece(Transform parent, string name, Material mat,
        float xMin, float xMax, float yMin, float yMax, float z, float thickness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Kapı Boşluğu Aç");   // Ctrl+Z ile geri alınabilir
#endif
        go.name = name;
        go.transform.position   = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, z);
        go.transform.localScale = new Vector3(Mathf.Max(0.01f, xMax - xMin), Mathf.Max(0.01f, yMax - yMin), thickness);
        go.isStatic = true;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.SetParent(parent, true);   // dünya pozisyonunu korur
        return go;
    }

    // ───────────────── 2) Kapı + cam + manzara (eklemeli) ─────────────────

    [ContextMenu("2) Giriş Cephesi Kur")]
    void BuildFacade()
    {
        if (!hasAnchor)
        {
            Debug.LogError("[EntranceFacadeBuilder] Önce '1) Kapı Boşluğu Aç' çalıştırılmalı " +
                            "(cephe merkezi henüz hesaplanmadı).", this);
            return;
        }

        var old = transform.Find(FacadeContainer);
        if (old) DestroyImmediate(old.gameObject);
        var root = new GameObject(FacadeContainer).transform;
        root.SetParent(transform, false);
        // DÜNYA konumu — bu objenin o anki transform'u ne olursa olsun, cephe hep 1. adımda
        // hesaplanan gerçek duvar-boşluğu merkezinde kurulur (sürüklenme/kayma korumalı).
        root.position = anchorWorldPos;
        root.rotation = Quaternion.identity;

        doorMat   = LitMat(doorColor, doorMetallic, doorSmoothness);
        glassMat  = TransparentMat(glassColor, glassMetallic, glassSmoothness);
        skyMat    = UnlitFlat(skyColor);
        windowMat = UnlitFlat(windowColor);

        BuildDoorLeaves(root);
        BuildGlassPanels(root);
        BuildNiche(root);
        BuildExteriorView(root);
        BuildSpotlight(root);

        Debug.Log("[EntranceFacadeBuilder] Giriş cephesi kuruldu.", this);
    }

    // Kapı kanatları — placeholder metal küpler + çerçeve + ortada ayrım şeridi (sadece
    // iki düz blok değil, "kapı" olarak okunsun diye) + mevcut ElevatorDoor component'i
    // (yeniden yazılmadı, Configure() ile programatik kuruldu). Trigger'a yaklaşınca
    // otomatik açılır — NOT: bu duvar resepsiyon masasının hemen arkasında, oyuncu
    // kapsülü (yarıçap 0.5) fiziksel olarak o dar aralığa giremediği için pratikte
    // tetiklenmeyebilir; bu durumda kapı sabit-kapalı bir vitrin elemanı gibi kalır,
    // zararsızdır.
    void BuildDoorLeaves(Transform root)
    {
        float leafWidth = doorWidth * 0.52f;   // ortada hafif bindirme (sızdırmaz görünüm)
        float leafY     = doorHeight * 0.5f;

        var doorRoot = new GameObject("Kapi").transform;
        doorRoot.SetParent(root, false);
        doorRoot.localPosition = Vector3.zero;

        var frameMat = LitMat(new Color(0.55f, 0.56f, 0.58f), 0.9f, 0.7f);   // parlak trim, kanatlardan daha açık

        // Çerçeve (bezel) — kanatların arkasında, biraz daha büyük, açık renk trim
        MakeBox(doorRoot, "KapiCercevesi", new Vector3(0f, leafY, -0.03f),
            new Vector3(doorWidth + 0.2f, doorHeight + 0.2f, doorThickness * 0.6f), frameMat, false);

        var left = MakeBox(doorRoot, "KapiKanadi_Sol", new Vector3(-leafWidth * 0.5f, leafY, 0f),
            new Vector3(leafWidth, doorHeight, doorThickness), doorMat, true);
        var right = MakeBox(doorRoot, "KapiKanadi_Sag", new Vector3(leafWidth * 0.5f, leafY, 0f),
            new Vector3(leafWidth, doorHeight, doorThickness), doorMat, true);

        // Ortada ince dikey ayrım şeridi — iki kanadın burada ayrıldığını gösterir,
        // "tek blob" değil "iki kanatlı kapı" gibi okunmasını sağlar.
        MakeBox(doorRoot, "KapiOrtaSeridi", new Vector3(0f, leafY, doorThickness * 0.5f + 0.005f),
            new Vector3(0.06f, doorHeight * 0.94f, 0.02f), frameMat, false);

        var ed = doorRoot.gameObject.AddComponent<ElevatorDoor>();   // RequireComponent → BoxCollider otomatik gelir
        var box = doorRoot.gameObject.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, leafY, 0f);
        box.size   = new Vector3(doorWidth, doorHeight, 1.4f);       // yaklaşma trigger'ı — biraz derin

        float openShift = leafWidth + 0.2f;
        ed.Configure(left.transform, right.transform,
            new Vector3(-openShift, 0f, 0f), new Vector3(openShift, 0f, 0f), true);
    }

    // Cam paneller — kapının iki yanında, tavana kadar, COLLIDER'LI (oyuncu içinden geçemez)
    void BuildGlassPanels(Transform root)
    {
        float glassCenterOffset = doorWidth * 0.5f + glassWidth * 0.5f;
        MakeBox(root, "CamPanel_Bati", new Vector3(-glassCenterOffset, wallHeight * 0.5f, 0f),
            new Vector3(glassWidth, wallHeight, 0.08f), glassMat, true);
        MakeBox(root, "CamPanel_Dogu", new Vector3(glassCenterOffset, wallHeight * 0.5f, 0f),
            new Vector3(glassWidth, wallHeight, 0.08f), glassMat, true);
    }

    // Küçük niş: kapı+cam önünde yürünebilir zemin yaması + ötesine geçilmesin diye
    // görünmez durdurucu (düşme güvenliği — bu duvarın ötesinde asıl oda zemini yok).
    void BuildNiche(Transform root)
    {
        float facadeWidth = doorWidth + glassWidth * 2f;
        Material fm = floorMatCache != null ? floorMatCache : FallbackMat(new Color(0.5f, 0.5f, 0.52f));

        MakeBox(root, "Zemin_Nis", new Vector3(0f, -wallThickness * 0.25f, nicheDepth * 0.5f),
            new Vector3(facadeWidth, wallThickness * 0.5f, nicheDepth), fm, true);

        var stopper = new GameObject("Durdurucu");
        stopper.transform.SetParent(root, false);
        stopper.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, nicheDepth);
        var box = stopper.AddComponent<BoxCollider>();
        box.size = new Vector3(facadeWidth, wallHeight, 0.2f);
    }

    // Dış manzara: bahçe (çim + ağaçlar, ÖN planda) + tek büyük bina (bahçenin ÖTESİNDE,
    // dominant) + her yönden kenarı görünmeyecek kadar büyük gökyüzü backdrop.
    // Sade, hareketsiz, collider'sız (sadece görsel) — kapı/cam ötesi manzara.
    void BuildExteriorView(Transform root)
    {
        float startZ = nicheDepth + skylineGap;
        float farZ   = startZ + skylineDepth;

        var view = new GameObject("DisManzara").transform;
        view.SetParent(root, false);

        // Gökyüzü — çok büyük, niş/camdan bakılan dar açıdan bile kenarı asla görünmesin.
        float skyWidth  = (doorWidth + glassWidth * 2f) * 14f;
        float skyHeight = wallHeight * 10f;
        MakeBox(view, "Gokyuzu", new Vector3(0f, skyHeight * 0.3f, farZ + skylineDepth * 0.5f),
            new Vector3(skyWidth, skyHeight, 0.2f), skyMat, false);

        // Çim zemini — niş sonundan gökyüzüne kadar geniş bir yeşil düzlem.
        var groundMat = UnlitFlat(groundColor);
        MakeBox(view, "Cim", new Vector3(0f, -0.05f, (startZ + farZ) * 0.5f),
            new Vector3(skyWidth, 0.1f, farZ - startZ + 4f), groundMat, false);

        var rng = new System.Random(seed);
        BuildGarden(view, startZ, startZ + gardenDepth, rng);
        BuildLargeBuilding(view, startZ + gardenDepth + 3f, rng);
    }

    // Bahçe: ağaçlar (gerçek model verilmişse treePrefab, yoksa gövde+yaprak greybox).
    void BuildGarden(Transform view, float zNear, float zFar, System.Random rng)
    {
        var garden = new GameObject("Bahce").transform;
        garden.SetParent(view, false);

        float halfWidth = (doorWidth + glassWidth * 2f) * 1.8f;
        for (int i = 0; i < treeCount; i++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * halfWidth;
            float z = Mathf.Lerp(zNear, zFar, (float)rng.NextDouble());
            float s = treeScale * Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble());
            var pos = new Vector3(x, 0f, z);

            if (treePrefab != null) InstancePrefab(treePrefab, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), s, garden);
            else                    GreyboxTree(garden, pos, s, $"Agac_{i}");
        }
    }

    // Greybox ağaç: gövde (silindir) + katmanlı yaprak (bastırılmış küreler) — gerçek
    // model verilmezse kullanılan placeholder (ReceptionBuilder'daki bitki deseniyle aynı).
    void GreyboxTree(Transform parent, Vector3 basePos, float s, string name)
    {
        var trunkMat = LitMat(treeTrunkColor, 0f, 0.2f);
        var leafMat  = LitMat(treeLeafColor, 0f, 0.15f);

        var tree = new GameObject(name).transform;
        tree.SetParent(parent, false);
        tree.localPosition = basePos;

        MakeBox(tree, "Govde", new Vector3(0f, 1.1f * s, 0f), new Vector3(0.35f * s, 2.2f * s, 0.35f * s), trunkMat, false);

        var l1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        l1.name = "Yaprak1"; DestroyImmediate(l1.GetComponent<Collider>());
        l1.transform.SetParent(tree, false);
        l1.transform.localPosition = new Vector3(0f, 2.6f * s, 0f);
        l1.transform.localScale    = new Vector3(2.6f, 1.9f, 2.6f) * s;
        l1.GetComponent<Renderer>().sharedMaterial = leafMat;

        var l2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        l2.name = "Yaprak2"; DestroyImmediate(l2.GetComponent<Collider>());
        l2.transform.SetParent(tree, false);
        l2.transform.localPosition = new Vector3(0.5f * s, 3.4f * s, -0.3f * s);
        l2.transform.localScale    = new Vector3(1.7f, 1.4f, 1.7f) * s;
        l2.GetComponent<Renderer>().sharedMaterial = leafMat;
    }

    // Tek, dominant büyük bina — bahçenin ötesinde. Basit iki katmanlı kütle (geniş taban +
    // dar üst) + pencere ızgarası.
    void BuildLargeBuilding(Transform view, float baseZ, System.Random rng)
    {
        var buildingMat = UnlitFlat(buildingColor);
        var building = new GameObject("BuyukBina").transform;
        building.SetParent(view, false);

        float baseH = buildingHeight * 0.62f;
        float topH  = buildingHeight - baseH;
        float z = baseZ + buildingDepth * 0.5f;

        MakeBox(building, "Taban", new Vector3(0f, baseH * 0.5f, z),
            new Vector3(buildingWidth, baseH, buildingDepth), buildingMat, false);
        MakeBox(building, "Ust", new Vector3(0f, baseH + topH * 0.5f, z),
            new Vector3(buildingWidth * 0.72f, topH, buildingDepth * 0.72f), buildingMat, false);

        if (addBuildingWindows)
        {
            AddBuildingWindows(building, 0f, z, buildingWidth, baseH, buildingDepth, rng);
            AddBuildingWindows(building, 0f, z, buildingWidth * 0.72f, topH, buildingDepth * 0.72f, rng, baseH);
        }
    }

    // Binanın oyuncuya bakan (−Z) yüzüne küçük, SABİT boyutlu pencere ızgarası. Binanın
    // KENDİ child'ı olarak DEĞİL, parent'a doğrudan dünya konumuyla eklenir — binanın
    // (w,h,d) ölçeği child'ın localScale'ini de çarpıp devasa/tutarsız pencerelere yol
    // açardı; bu yüzden konum/boyut burada elle, dünya uzayında hesaplanır.
    void AddBuildingWindows(Transform parent, float bx, float bz, float w, float h, float d, System.Random rng, float yOffset = 0f)
    {
        float faceZ = bz - d * 0.5f - 0.03f;   // binanın niş'e/oyuncuya bakan ön yüzü
        int cols = Mathf.Max(1, Mathf.RoundToInt(w / 1.6f));
        int rows = Mathf.Max(1, Mathf.RoundToInt(h / 2.2f));

        for (int cx = 0; cx < cols; cx++)
        for (int cy = 0; cy < rows; cy++)
        {
            float wx = bx - w * 0.5f + w * (cx + 0.5f) / cols;
            float wy = yOffset + h * (cy + 0.5f) / rows;

            var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
            win.name = "Pencere";
            DestroyImmediate(win.GetComponent<Collider>());
            win.transform.SetParent(parent, false);
            win.transform.position   = new Vector3(wx, wy, faceZ);
            win.transform.localScale = new Vector3(0.9f, 1.3f, 0.02f);   // sabit boyut — bina ölçeğinden bağımsız
            win.GetComponent<Renderer>().sharedMaterial = windowMat;
        }
    }

    void InstancePrefab(GameObject prefab, Vector3 localPos, Quaternion localRot, float scale, Transform parent)
    {
        var go = Instantiate(prefab, parent);
        go.name = prefab.name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale    = prefab.transform.localScale * scale;
    }

    // Girişi vurgulayan spot — oda içi tarafta (negatif Z), kapıya doğru aşağı bakar.
    void BuildSpotlight(Transform root)
    {
        var lgo = new GameObject("GirisSpotIsigi");
        lgo.transform.SetParent(root, false);
        lgo.transform.localPosition = new Vector3(0f, wallHeight - 0.3f, -1.2f);
        lgo.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

        var light   = lgo.AddComponent<Light>();
        light.type  = LightType.Spot;
        light.color = spotColor;
        light.range = spotRange;
        light.spotAngle = spotAngle;
        var hd = lgo.AddComponent<HDAdditionalLightData>();
        hd.SetIntensity(spotLumen, LightUnit.Lumen);
        hd.EnableShadows(false);
    }

    // ───────────────── Yardımcılar ─────────────────

    GameObject MakeBox(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) { var c = go.GetComponent<Collider>(); if (c) DestroyImmediate(c); }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        go.isStatic = true;
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    Material LitMat(Color c, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    // HDRP/Lit'in Transparent moduna geçmesi çok sayıda birbirine bağlı keyword/blend/
    // render-queue ayarı gerektirir (Editor'daki Surface Type açılır menüsünün arkasında
    // olan şey) — bunu kod içinden elle/kör ayarlamak kırılgan (yanlış kombinasyon derlenir
    // ama render bozuk çıkar). Bu yüzden BURADA OPAK bir HDRP/Lit üretiliyor; camı gerçekten
    // saydam yapmak için Unity'de bu materyali (CamPanel_Bati/Dogu'nun kullandığı — iki panel
    // de AYNI materyali paylaşıyor, tek seferde ikisi de düzelir) seçip Inspector'da
    // Surface Type = Transparent, Blending Mode = Alpha işaretle. Renk/alpha/Smoothness
    // değerleri zaten doğru ayarlanmış geliyor, sadece bu tek anahtarı çevirmen yeterli.
    Material TransparentMat(Color c, float metallic, float smoothness)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    Material UnlitFlat(Color c)
    {
        var m = new Material(Shader.Find("HDRP/Unlit"));
        m.SetColor("_UnlitColor", c);
        return m;
    }

    Material FallbackMat(Color c)
    {
        var m = new Material(Shader.Find("HDRP/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    void OnDrawGizmosSelected()
    {
        // Anchor hesaplanmışsa (1. adım çalıştıysa) ONU göster — bu objenin o anki
        // transform'u kaymış olsa bile gizmo hep gerçek kapı yerini gösterir.
        Vector3 basePos = hasAnchor ? anchorWorldPos : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(basePos + Vector3.up * doorHeight * 0.5f,
            new Vector3(doorWidth, doorHeight, 0.3f));
    }
}
}
