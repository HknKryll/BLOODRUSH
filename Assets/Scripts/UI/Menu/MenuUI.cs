using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Bloodrush.UI.Menu
{
// Menunun butun yapi taslari. Her sey MenuTheme'den okur — kodun icinde ham renk yok.
//
// TASARIM KARARI — ACILIR LISTE (Dropdown) YOK:
// Cozunurluk/ekran modu/kalite gibi secimler "< Deger >" adimlayici olarak kuruluyor ve
// bunlar aslinda wholeNumbers=true bir Unity Slider'i. Sebep: Slider klavye ok tuslarini
// ve gamepad sol cubugu/d-pad'i KENDILIGINDEN destekliyor; TMP_Dropdown ise acilinca
// gamepad odagini kaybediyor ve kurumsal/minimal gorunume de uymuyor. Boylece hem tek
// kontrol tipi hem bedava gamepad destegi.
public static class MenuUI
{
    // ───────────────── Iskelet ─────────────────

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // YUKSEKLIGE gore olcekle (match=1): 21:9 ultra-genis ekranda arayuz ayni fiziksel
        // boyutta kalir. 0.5 kullanilsaydi ultra-genis ekranda her sey kuculurdu.
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 1f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Stretch(Transform parent, string name, Vector2 padMin = default, Vector2 padMax = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = padMin;
        rt.offsetMax = padMax;
        return rt;
    }

    public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        => Box(parent, name, anchor, pos, size, new Vector2(0.5f, 0.5f));

    public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos,
                                    Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot     = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }

    // Ebeveynin ICINE kenar bosluklariyla oturan esnek alan. Anchor+pivot aritmetigi
    // yerine dogrudan kenar payi verdigi icin panel disina TASMAZ ve her cozunurlukte
    // dogru olcekler.
    public static RectTransform Inset(Transform parent, string name,
                                      float left, float right, float top, float bottom)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static Image Fill(Transform parent, string name, Color color)
    {
        var rt  = Stretch(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ───────────────── Metin ─────────────────

    public static TextMeshProUGUI Text(Transform parent, string content, float size, Color color,
        float tracking = 0f, TextAlignmentOptions align = TextAlignmentOptions.Left,
        TMP_FontAsset font = null, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = content;
        tmp.fontSize           = size;
        tmp.color              = color;
        tmp.characterSpacing   = tracking;
        tmp.alignment          = align;
        tmp.fontStyle          = style;
        tmp.raycastTarget      = false;
        tmp.enableWordWrapping = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    // Ince ayrac — kurumsal sunum dilinin belkemigi.
    public static Image Hairline(Transform parent, MenuTheme theme, Vector2 anchor, Vector2 pos, float width)
    {
        var rt  = Box(parent, "Hairline", anchor, pos, new Vector2(width, 1f));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = theme.hairline;
        img.raycastTarget = false;
        return img;
    }

    // ───────────────── Buton ─────────────────

    public static MenuButton Button(Transform parent, MenuTheme theme, string label,
        Vector2 anchor, Vector2 pos, Vector2 size, Action onClick,
        TextAlignmentOptions align = TextAlignmentOptions.Left, Color? textColor = null)
    {
        var rt = Box(parent, "Btn_" + label, anchor, pos, size);

        // Tiklama alani — gorunmez ama raycast alir (kutu cizmiyoruz, metin butonu).
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        var fill = Fill(rt, "Fill", new Color(0f, 0f, 0f, 0f));

        // Odak halkasi (klavye/gamepad): DORT INCE KENAR.
        // Tam ekran bir Image + Outline kullanmak butun butonu altin dolduruyordu —
        // Outline grafigin KOPYASINI cizer, grafigin kendisini gizlemez.
        var ringRT = Stretch(rt, "Ring");
        var ringCG = ringRT.gameObject.AddComponent<CanvasGroup>();
        ringCG.alpha = 0f;
        ringCG.blocksRaycasts = false;
        Edges(ringRT, theme.focusRing, theme.focusRingWidth);

        // Solda beliren altin cubuk
        var barRT = Box(rt, "Bar", new Vector2(0f, 0.5f),
                        new Vector2(theme.accentBarWidth * 0.5f, 0f),
                        new Vector2(theme.accentBarWidth, size.y * 0.6f));
        var bar = barRT.gameObject.AddComponent<Image>();
        bar.color = new Color(0f, 0f, 0f, 0f);
        bar.raycastTarget = false;

        float padLeft = theme.U2;
        var labelRT = Stretch(rt, "Label", new Vector2(padLeft, 0f), new Vector2(-theme.U1, 0f));
        var tmp = Text(labelRT, label, theme.sizeLabel, textColor ?? theme.textPrimary,
                       theme.trackingLabel, align, theme.bodyFont);

        var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.transition    = Selectable.Transition.None;
        btn.targetGraphic = hit;
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        var mb = rt.gameObject.AddComponent<MenuButton>();
        mb.Init(theme, fill, bar, ringCG, tmp);
        return mb;
    }

    // Bir dikdortgenin dort kenarina ince cizgi koyar (cerceve).
    public static void Edges(RectTransform parent, Color color, float width)
    {
        void Edge(string name, Vector2 aMin, Vector2 aMax, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        Edge("T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, width));
        Edge("B", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, width));
        Edge("L", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(width, 0f));
        Edge("R", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(width, 0f));
    }

    // ───────────────── Satir (etiket solda, kontrol sagda) ─────────────────

    public const float RowHeight = 44f;

    // PIVOT USTTE (0.5, 1): y ofseti dogrudan "ust kenardan su kadar asagi" demek olur.
    // Merkez pivotla satirin ust yarisi alanin disina tasiyordu.
    public static RectTransform Row(Transform parent, MenuTheme theme, string label,
        float y, float width, out TextMeshProUGUI labelText)
    {
        var rt = Box(parent, "Row_" + label, new Vector2(0f, 1f),
                     new Vector2(0f, y), new Vector2(width, RowHeight),
                     new Vector2(0f, 1f));

        // Etiket solda, kontrol sagda — ikisi de esnek genislikte, sabit oran degil.
        var labelRT = Inset(rt, "L", 0f, width * 0.5f, 0f, 0f);
        labelText = Text(labelRT, label, theme.sizeBody, theme.textPrimary, 2f,
                         TextAlignmentOptions.Left, theme.bodyFont);

        var controlRT = Inset(rt, "C", width * 0.44f, 0f, 0f, 0f);

        // Alt ayrac
        var hl = Box(rt, "Hairline", new Vector2(0f, 0f), new Vector2(0f, 0f),
                     new Vector2(width, 1f), new Vector2(0f, 0f));
        var img = hl.gameObject.AddComponent<Image>();
        img.color = theme.hairline;
        img.raycastTarget = false;

        return controlRT;
    }

    // ───────────────── Kaydirici / Adimlayici ─────────────────

    // wholeNumbers=true + formatter verilirse "< Deger >" adimlayici gibi gorunur.
    // Her iki durumda da Unity Slider oldugu icin ok tuslari ve gamepad bedava calisir.
    // Yatayda esneyen, sabit yukseklikli serit. Sabit piksel ofsetleri yerine kenar payi
    // kullandigi icin satir genisligi ne olursa olsun tasmaz.
    public static RectTransform HBand(Transform parent, string name, float left, float right, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left,  -height * 0.5f);
        rt.offsetMax = new Vector2(-right, height * 0.5f);
        return rt;
    }

    public static Slider SliderControl(Transform parent, MenuTheme theme,
        float min, float max, float value, bool wholeNumbers,
        Func<float, string> format, Action<float> onChange, bool showTrack = true)
    {
        var rt = Stretch(parent, "Slider");
        var slider = rt.gameObject.AddComponent<Slider>();
        slider.minValue     = min;
        slider.maxValue     = max;
        slider.wholeNumbers = wholeNumbers;
        slider.transition   = Selectable.Transition.None;

        // Sagda deger metni icin ayrilan pay.
        const float ValueW = 86f;

        if (showTrack)
        {
            HBand(rt, "Track", 0f, ValueW + 12f, 2f)
                .gameObject.AddComponent<Image>().color = theme.hairline;

            var fillArea = HBand(rt, "FillArea", 0f, ValueW + 12f, 2f);
            var fillRT   = Stretch(fillArea, "Fill");
            var fillImg  = fillRT.gameObject.AddComponent<Image>();
            fillImg.color = theme.accent;
            slider.fillRect = fillRT;

            // Sap: kucuk altin dikdortgen — minimal.
            var handleArea = HBand(rt, "HandleArea", 0f, ValueW + 12f, 16f);
            var handleRT   = Box(handleArea, "Handle", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 16f));
            var handleImg  = handleRT.gameObject.AddComponent<Image>();
            handleImg.color = theme.accent;
            slider.handleRect    = handleRT;
            slider.targetGraphic = handleImg;
        }

        // Deger metni: kaydiricida sagda sabit paylik alan, adimlayicida butun genislik.
        var valueRT = showTrack
            ? Box(rt, "Value", new Vector2(1f, 0.5f), Vector2.zero,
                  new Vector2(ValueW, 28f), new Vector2(1f, 0.5f))
            : HBand(rt, "Value", 0f, 0f, 28f);

        var valueTxt = Text(valueRT, "", theme.sizeBody, theme.accentText, 3f,
                            TextAlignmentOptions.Right, theme.bodyFont);

        slider.value = Mathf.Clamp(value, min, max);
        valueTxt.text = format != null ? format(slider.value) : slider.value.ToString("0.00");

        slider.onValueChanged.AddListener(v =>
        {
            valueTxt.text = format != null ? format(v) : v.ToString("0.00");
            onChange?.Invoke(v);
        });

        return slider;
    }

    // ───────────────── Animasyon ─────────────────

    // Fade + kisa yukari kayma. Zipla/esne yok — kullanicinin acik istegi.
    public static System.Collections.IEnumerator FadeSlideIn(CanvasGroup cg, RectTransform rt,
        float distance, float duration, float delay = 0f)
    {
        if (cg == null) yield break;
        cg.alpha = 0f;
        Vector2 target = rt != null ? rt.anchoredPosition : Vector2.zero;
        Vector2 start  = target - new Vector2(0f, distance);
        if (rt != null) rt.anchoredPosition = start;

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            cg.alpha = p;
            if (rt != null) rt.anchoredPosition = Vector2.Lerp(start, target, p);
            yield return null;
        }
        cg.alpha = 1f;
        if (rt != null) rt.anchoredPosition = target;
    }

    public static System.Collections.IEnumerator FadeOut(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;
        float start = cg.alpha, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
            yield return null;
        }
        cg.alpha = 0f;
    }
}
}
