using UnityEngine;
using UnityEngine.UI;

// Paylaşılan tutorial ipucu göstergesi. TutorialHint bölgeleri bunu çağırır.
// İlk çağrıda kendini oluşturur (DamageVignette deseni). Ekranın alt-ortasında
// yumuşak fade'li bir satır gösterir.
using Bloodrush.FX;

namespace Bloodrush.Flow
{
public class TutorialHintUI : MonoBehaviour
{
    static TutorialHintUI instance;

    CanvasGroup group;
    Text        label;
    float       targetAlpha;

    public static void Show(string text)
    {
        Ensure();
        instance.label.text   = text;
        instance.targetAlpha  = 1f;
    }

    public static void Hide()
    {
        if (instance == null) return;   // Unity fake-null: sahne değişince yeniden kurulur
        instance.targetAlpha = 0f;
    }

    // Duraklatma menüsü açılıp kapanınca çağrılır — açıkken ipucu tamamen
    // gizlenir (donuk halde pause menüsünün üstünde kalmasın), kapanınca
    // kaldığı yerden (varsa) devam eder.
    public static void SetPaused(bool paused)
    {
        if (instance == null) return;
        instance.gameObject.SetActive(!paused);
    }

    static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("TutorialHintUI");
        instance = go.AddComponent<TutorialHintUI>();
        instance.Build();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;
        gameObject.AddComponent<CanvasScaler>();

        var panelGO = new GameObject("HintPanel");
        panelGO.transform.SetParent(transform, false);
        group = panelGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.15f, 0.14f);
        panelRT.anchorMax = new Vector2(0.85f, 0.22f);
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;

        // Yarı saydam koyu arka plan
        var bg = new GameObject("BG").AddComponent<Image>();
        bg.transform.SetParent(panelGO.transform, false);
        bg.color = new Color(0.03f, 0.03f, 0.03f, 0.7f);
        bg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        label = new GameObject("Label").AddComponent<Text>();
        label.transform.SetParent(panelGO.transform, false);
        label.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize  = 20;
        label.color     = new Color(0.95f, 0.95f, 0.95f, 1f);
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        var lblRT = label.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(12f, 6f);
        lblRT.offsetMax = new Vector2(-12f, -6f);
    }

    void Update()
    {
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * 6f);
    }
}
}
