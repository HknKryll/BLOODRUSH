using UnityEngine;
using UnityEngine.UI;
using Bloodrush.Shared;

namespace Bloodrush.UI
{
// Boss can barı — ekran üst-ortasında geniş bir bar (isim + kırmızı dolgu).
// Statik ShowBoss/HideBoss ile sürülür; UI yoksa kendini oluşturur (kullanıcı
// sahneye bir şey koymaz). Boss ölünce/null olunca gizlenir.
public class BossHealthUI : MonoBehaviour
{
    static BossHealthUI instance;

    Health        boss;
    GameObject     root;
    RectTransform  fillRT;
    Text           nameText;

    public static void ShowBoss(Health health, string bossName)
    {
        if (health == null) return;
        if (instance == null)
            instance = new GameObject("BossHealthUI").AddComponent<BossHealthUI>();
        instance.Set(health, bossName);
    }

    public static void HideBoss()
    {
        if (instance != null) instance.Clear();
    }

    void Awake()
    {
        instance = this;
        BuildUI();
        root.SetActive(false);
    }

    void Set(Health h, string bossName)
    {
        boss = h;
        if (nameText) nameText.text = bossName;
        root.SetActive(true);
    }

    void Clear()
    {
        boss = null;
        if (root) root.SetActive(false);
    }

    void Update()
    {
        if (boss == null) return;
        if (boss.Current <= 0f) { Clear(); return; }
        float frac = boss.Max > 0f ? Mathf.Clamp01(boss.Current / boss.Max) : 0f;
        var am = fillRT.anchorMax;
        am.x = frac;
        fillRT.anchorMax = am;
    }

    void BuildUI()
    {
        var cgo    = new GameObject("BossHealthCanvas");
        root       = cgo;
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        // Üst-orta bar
        var barGO = new GameObject("BossBar");
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.SetParent(cgo.transform, false);
        barRT.anchorMin        = new Vector2(0.5f, 1f);
        barRT.anchorMax        = new Vector2(0.5f, 1f);
        barRT.pivot            = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = new Vector2(0f, -24f);
        barRT.sizeDelta        = new Vector2(760f, 24f);
        var bg = barGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // Dolgu (anchor tabanlı — genişlik anchorMax.x ile)
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(barGO.transform, false);
        var fill = fillGO.AddComponent<Image>();
        fill.color = new Color(0.85f, 0.12f, 0.12f, 1f);   // kırmızı
        fill.raycastTarget = false;
        fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // İsim (barın üstünde)
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(barGO.transform, false);
        nameText = nameGO.AddComponent<Text>();
        nameText.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize      = 18;
        nameText.fontStyle     = FontStyle.Bold;
        nameText.color         = new Color(1f, 0.9f, 0.9f, 1f);
        nameText.alignment     = TextAnchor.LowerCenter;
        nameText.raycastTarget = false;
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin        = new Vector2(0f, 1f);
        nameRT.anchorMax        = new Vector2(1f, 1f);
        nameRT.pivot            = new Vector2(0.5f, 0f);
        nameRT.anchoredPosition = new Vector2(0f, 4f);
        nameRT.sizeDelta        = new Vector2(0f, 24f);
    }
}
}
