using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bloodrush.Flow
{
// Valf mini oyununun ekrandaki yuvarlak basinc saati + los durum satiri. Sadece SUNUM:
// ValveMiniGame ve ValveSequence event'lerini dinler, mantiga dokunmaz.
//
// Dokular (disk/halka) calisma zamaninda uretilir; asset gerekmez. Yesil bolge, ayni halkanin
// Image.Filled Radial360 ile kesilmis bir dilimi. Canvas sort 16: oyun HUD'u bandinda, pause
// arka planinin (190) altinda.
public class ValveGaugeUI : MonoBehaviour
{
    const int   SortingOrder = 16;
    const float Diameter     = 230f;

    static readonly Color FaceColor   = new Color(0.06f, 0.06f, 0.065f, 0.88f);
    static readonly Color RimColor    = new Color(0.32f, 0.33f, 0.35f, 1f);
    static readonly Color TrackColor  = new Color(0.16f, 0.18f, 0.16f, 0.95f);
    static readonly Color ZoneColor   = new Color(0.33f, 0.72f, 0.36f, 1f);
    static readonly Color ZoneHit     = new Color(0.62f, 1f, 0.62f, 1f);
    static readonly Color NeedleColor = new Color(1f, 0.62f, 0.22f, 1f);
    static readonly Color MissColor   = new Color(0.92f, 0.2f, 0.14f, 1f);
    static readonly Color TextColor   = new Color(0.86f, 0.83f, 0.76f, 0.85f);
    static readonly Color BoxEmpty    = new Color(0.22f, 0.22f, 0.24f, 0.95f);

    static Texture2D discTex, rimTex, trackTex, squareTex;
    static Sprite    discSprite, rimSprite, trackSprite, squareSprite;

    ValveMiniGame game;
    ValveSequence sequence;

    GameObject    canvasGo;
    CanvasGroup   gaugeGroup;
    RectTransform needle, zoneRt;
    Image         rim, zone, flash, timerFill;
    GameObject    timerRoot;
    Text          caption, status;
    CanvasGroup   statusGroup;
    RectTransform boxRow;
    Image[]       boxes = new Image[0];

    Coroutine gaugeFade, statusFade, feedback, flashFade;
    float     targetAlpha;

    public void Bind(ValveMiniGame miniGame, ValveSequence seq)
    {
        Unbind();
        game = miniGame;
        sequence = seq;
        if (canvasGo == null) Build();

        game.Started += OnStarted;
        game.Checked += OnChecked;
        game.Ended   += OnEnded;
        sequence.Status += ShowStatus;
    }

    void Unbind()
    {
        if (game != null)
        {
            game.Started -= OnStarted;
            game.Checked -= OnChecked;
            game.Ended   -= OnEnded;
        }
        if (sequence != null) sequence.Status -= ShowStatus;
    }

    void OnDestroy()
    {
        Unbind();
        if (canvasGo != null) Destroy(canvasGo);
    }

    // ── Event'ler ──────────────────────────────────────────────────────

    void OnStarted(ValveInteractable valve)
    {
        caption.text = valve.DisplayName;
        BuildBoxes(game.Required);
        rim.color  = RimColor;
        zone.color = ZoneColor;
        Fade(1f);
    }

    void OnChecked(bool hit)
    {
        RefreshBoxes();
        if (feedback != null) StopCoroutine(feedback);
        feedback = StartCoroutine(hit ? HitPulse() : MissPulse());
        if (!hit)
        {
            if (flashFade != null) StopCoroutine(flashFade);
            flashFade = StartCoroutine(WhiteFlash());
        }
    }

    void OnEnded(ValveInteractable valve, bool opened) => Fade(0f);

    public void ShowStatus(string text)
    {
        status.text = text;
        if (statusFade != null) StopCoroutine(statusFade);
        statusFade = StartCoroutine(StatusRoutine());
    }

    // ── Kare guncellemesi ──────────────────────────────────────────────

    void LateUpdate()
    {
        if (game == null || !game.IsActive) return;

        needle.localRotation = Quaternion.Euler(0f, 0f, -game.NeedleAngle);
        zone.fillAmount      = Mathf.Clamp01(game.ZoneWidth / 360f);
        zoneRt.localRotation = Quaternion.Euler(0f, 0f, -(game.ZoneCenter - game.ZoneWidth * 0.5f));

        float remaining = sequence != null ? sequence.RemainingFraction : -1f;
        bool showTimer = remaining >= 0f;
        if (timerRoot.activeSelf != showTimer) timerRoot.SetActive(showTimer);
        if (showTimer)
        {
            timerFill.fillAmount = remaining;
            timerFill.color = Color.Lerp(MissColor, NeedleColor, remaining);
        }
    }

    // ── Animasyonlar ───────────────────────────────────────────────────

    void Fade(float to)
    {
        targetAlpha = to;
        if (gaugeFade != null) StopCoroutine(gaugeFade);
        gaugeFade = StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        while (!Mathf.Approximately(gaugeGroup.alpha, targetAlpha))
        {
            gaugeGroup.alpha = Mathf.MoveTowards(gaugeGroup.alpha, targetAlpha, Time.unscaledDeltaTime / 0.15f);
            yield return null;
        }
        gaugeFade = null;
    }

    IEnumerator HitPulse()
    {
        zone.color = ZoneHit;
        yield return new WaitForSecondsRealtime(0.15f);
        zone.color = ZoneColor;
        feedback = null;
    }

    IEnumerator MissPulse()
    {
        for (int i = 0; i < 3; i++)
        {
            rim.color = MissColor; zone.color = MissColor;
            yield return new WaitForSecondsRealtime(0.1f);
            rim.color = RimColor;  zone.color = ZoneColor;
            yield return new WaitForSecondsRealtime(0.08f);
        }
        feedback = null;
    }

    IEnumerator WhiteFlash()
    {
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0f, t / 0.4f));
            yield return null;
        }
        flash.color = Color.clear;
        flashFade = null;
    }

    IEnumerator StatusRoutine()
    {
        float t = 0f;
        while (t < 0.25f) { t += Time.unscaledDeltaTime; statusGroup.alpha = t / 0.25f; yield return null; }
        statusGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.2f);
        t = 0f;
        while (t < 0.6f) { t += Time.unscaledDeltaTime; statusGroup.alpha = 1f - t / 0.6f; yield return null; }
        statusGroup.alpha = 0f;
        statusFade = null;
    }

    // ── Kurulum ────────────────────────────────────────────────────────

    void Build()
    {
        EnsureSprites();

        canvasGo = new GameObject("ValveGaugeUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        // Iska flasi en altta, tam ekran.
        flash = Img(canvasGo.transform, "BeyazFlas", null, Color.clear);
        Stretch(flash.rectTransform);

        // Gosterge
        var root = Rect(canvasGo.transform, "Gosterge", new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(Diameter, Diameter));
        gaugeGroup = root.gameObject.AddComponent<CanvasGroup>();
        gaugeGroup.alpha = 0f;
        gaugeGroup.interactable = gaugeGroup.blocksRaycasts = false;

        Fill(Img(root, "Yuz", discSprite, FaceColor));
        rim = Img(root, "Cerceve", rimSprite, RimColor);
        Fill(rim);
        Fill(Img(root, "Yol", trackSprite, TrackColor));

        zone = Img(root, "YesilBolge", trackSprite, ZoneColor);
        zone.type          = Image.Type.Filled;
        zone.fillMethod    = Image.FillMethod.Radial360;
        zone.fillOrigin    = (int)Image.Origin360.Top;
        zone.fillClockwise = true;
        zoneRt = zone.rectTransform;
        Fill(zone);

        for (int i = 0; i < 12; i++)   // centikler
        {
            var tick = Img(root, "Centik", null, new Color(0.55f, 0.55f, 0.58f, 0.7f)).rectTransform;
            tick.pivot = new Vector2(0.5f, 0f);
            tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 0.5f);
            tick.sizeDelta = new Vector2(i % 3 == 0 ? 3f : 2f, i % 3 == 0 ? 10f : 6f);
            tick.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            // Yolun (0.62-0.78) disinda, cercevenin (0.88) icinde kalsin: bolgeyi ortmesin.
            tick.anchoredPosition = tick.localRotation * new Vector3(0f, Diameter * 0.5f * 0.87f - tick.sizeDelta.y, 0f);
        }

        needle = Img(root, "Ibre", null, NeedleColor).rectTransform;
        needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot     = new Vector2(0.5f, 0.05f);
        needle.sizeDelta = new Vector2(5f, Diameter * 0.5f * 0.82f);
        needle.anchoredPosition = Vector2.zero;

        var hub = Img(root, "Gobek", discSprite, new Color(0.45f, 0.45f, 0.48f, 1f)).rectTransform;
        hub.anchorMin = hub.anchorMax = new Vector2(0.5f, 0.5f);
        hub.sizeDelta = new Vector2(22f, 22f);

        caption = Label(root, "Baslik", new Vector2(0f, Diameter * 0.5f + 44f), 20, TextAnchor.MiddleCenter);
        Label(root, "Ipucu", new Vector2(0f, -Diameter * 0.5f - 58f), 18, TextAnchor.MiddleCenter).text = "[E]  yeşildeyken bas";

        boxRow = Rect(root, "Basarilar", new Vector2(0.5f, 0.5f), new Vector2(0f, -Diameter * 0.5f - 26f), new Vector2(200f, 16f));

        timerRoot = Rect(root, "Sure", new Vector2(0.5f, 0.5f), new Vector2(0f, Diameter * 0.5f + 18f), new Vector2(200f, 6f)).gameObject;
        Fill(Img(timerRoot.transform, "Zemin", null, new Color(0.15f, 0.15f, 0.16f, 0.85f)));
        timerFill = Img(timerRoot.transform, "Dolgu", null, NeedleColor);
        timerFill.sprite     = squareSprite;   // Filled tipi sprite olmadan cizilmez
        timerFill.type       = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        Fill(timerFill);
        timerRoot.SetActive(false);

        // Los durum satiri — mini oyun disinda da gorunur.
        var statusRt = Rect(canvasGo.transform, "Durum", new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(900f, 30f));
        statusGroup = statusRt.gameObject.AddComponent<CanvasGroup>();
        statusGroup.alpha = 0f;
        statusGroup.interactable = statusGroup.blocksRaycasts = false;
        status = Label(statusRt, "Yazi", Vector2.zero, 22, TextAnchor.MiddleCenter);
    }

    void BuildBoxes(int count)
    {
        foreach (var b in boxes) if (b != null) Destroy(b.gameObject);
        boxes = new Image[count];
        const float size = 14f, gap = 8f;
        float total = count * size + (count - 1) * gap;
        for (int i = 0; i < count; i++)
        {
            var img = Img(boxRow, "Kutu", null, BoxEmpty);
            var rt  = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-total * 0.5f + size * 0.5f + i * (size + gap), 0f);
            boxes[i] = img;
        }
    }

    void RefreshBoxes()
    {
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i] != null) boxes[i].color = i < game.Successes ? NeedleColor : BoxEmpty;
    }

    static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color  = color;
        img.raycastTarget = false;
        return img;
    }

    static void Fill(Image img) => Stretch(img.rectTransform);

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text Label(Transform parent, string name, Vector2 pos, int fontSize, TextAnchor align)
    {
        var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), pos, new Vector2(600f, fontSize + 10f));
        var txt = rt.gameObject.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.color     = TextColor;
        txt.alignment = align;
        txt.raycastTarget      = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;
        return txt;
    }

    // ── Prosedurel dokular ─────────────────────────────────────────────

    static void EnsureSprites()
    {
        if (discSprite != null) return;
        discTex  = RingTexture(256, -1f,  1f);
        rimTex   = RingTexture(256, 0.88f, 1f);
        trackTex = RingTexture(256, 0.62f, 0.78f);
        squareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "ValveGauge_Square" };
        var white = new Color32[16];
        for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
        squareTex.SetPixels32(white);
        squareTex.Apply();

        discSprite   = ToSprite(discTex);
        rimSprite    = ToSprite(rimTex);
        trackSprite  = ToSprite(trackTex);
        squareSprite = ToSprite(squareTex);
    }

    static Texture2D RingTexture(int size, float inner, float outer)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ValveGauge_Ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        float half = size * 0.5f;
        float px   = 1f / half;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
            float a = Mathf.Clamp01((outer - d) / px + 0.5f) * Mathf.Clamp01((d - inner) / px + 0.5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    static Sprite ToSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
}
}
