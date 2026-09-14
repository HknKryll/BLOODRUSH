using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Bloodrush.Flow;
using Bloodrush.Shared.Audio;

namespace Bloodrush.UI
{
public class MainMenuManager : MonoBehaviour
{
    // ─── Palet ───────────────────────────────────────────────────────
    static readonly Color BgColor     = new Color(0.051f, 0.051f, 0.051f, 1f);
    static readonly Color NeonRed     = new Color(1f, 0.102f, 0.102f, 1f);
    static readonly Color NeonRedGlow = new Color(1f, 0.102f, 0.102f, 0.45f);
    static readonly Color NeonRedFill = new Color(1f, 0.102f, 0.102f, 0.28f);
    static readonly Color BtnBg       = new Color(0.04f, 0.04f, 0.04f, 0.93f);
    static readonly Color ScanLine    = new Color(0f, 0f, 0f, 0.13f);

    GameObject mainPanel;
    GameObject settingsPanel;

    Slider          volumeSlider;
    TextMeshProUGUI volumeLabel;
    Slider          sensSlider;
    TextMeshProUGUI sensLabel;

    RectTransform titleRT;
    CanvasGroup   titleCG;

    readonly List<(RectTransform rt, CanvasGroup cg)> btnAnims = new();

    // ─── Başlangıç ───────────────────────────────────────────────────

    void Start()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        BuildUI();
        StartCoroutine(AnimateIn());
    }

    // ─── Aksiyonlar ──────────────────────────────────────────────────

    [SerializeField] string firstSceneName = "CH1";

    [Header("Menü Müziği")]
    [Tooltip("Menüdeki RoomMusic objesi. Boş bırakılırsa sahnede aranır.")]
    [SerializeField] RoomMusic menuMusic;
    [Tooltip("BAŞLAT'a basınca müziğin ve ekranın sönme süresi (sn).")]
    [SerializeField] float exitFadeDuration = 1.5f;
    [Tooltip("Sonraki sahne siyahtan açılırken kullanılacak süre (sn).")]
    [SerializeField] float nextSceneFadeIn = 0.6f;

    bool starting;

    void StartGame()
    {
        if (starting) return;      // çift tıklama korumasi
        starting = true;
        StartCoroutine(StartGameRoutine());
    }

    // Müzik sönerken sahne ARKA PLANDA yüklenir; yani 1.5 sn'lik fade bir bekleme
    // degil, zaten harcanacak yükleme süresinin üstüne biniyor. Fade bitince perde
    // SceneFadeIn.CarryOverlay ile sonraki sahneye taşınır ve orada açılır — oyuncu
    // menüden oyuna kesintisiz, karadan geçer.
    IEnumerator StartGameRoutine()
    {
        GameHUD.ResetProgress();   // yeni oyun = upload sıfır

        var music = menuMusic != null ? menuMusic : FindObjectOfType<RoomMusic>();
        if (music != null) music.FadeOut(exitFadeDuration);

        var op = SceneManager.LoadSceneAsync(firstSceneName);
        if (op == null)
        {
            Debug.LogError($"[MainMenuManager] '{firstSceneName}' yüklenemedi — " +
                           "Build Settings'te ekli mi?", this);
            starting = false;
            yield break;
        }
        op.allowSceneActivation = false;

        var img = GameFlow.CreateOverlay(Color.black);
        float t = 0f;
        while (t < exitFadeDuration)
        {
            t += Time.unscaledDeltaTime;   // menüde timeScale'e güvenme (AnimateIn de öyle)
            img.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / exitFadeDuration));
            yield return null;
        }
        img.color = Color.black;

        // allowSceneActivation=false iken progress 0.9'da takılır — "hazır" demektir.
        while (op.progress < 0.9f) yield return null;

        SceneFadeIn.CarryOverlay(img, 0f, nextSceneFadeIn);
        op.allowSceneActivation = true;
    }

    void ExitGame()      => Application.Quit();

    void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    void OnVolumeChanged(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat("MasterVolume", v);
        if (volumeLabel) volumeLabel.text = $"SES   {v:0.0}";
    }

    void OnSensChanged(float v)
    {
        PlayerPrefs.SetFloat("Sensitivity", v);
        if (sensLabel) sensLabel.text = $"HASSASİYET   {v:0.0}";
    }

    // ─── Giriş Animasyonu ────────────────────────────────────────────

    IEnumerator AnimateIn()
    {
        // Başlangıç durumu
        titleCG.alpha = 0f;
        titleRT.anchoredPosition -= new Vector2(0, 30f);
        foreach (var (rt, cg) in btnAnims) { cg.alpha = 0f; rt.anchoredPosition -= new Vector2(0, 15f); }

        // Başlık
        yield return StartCoroutine(FadeMove(titleCG, titleRT, 30f, 0.5f));

        // Butonlar sırayla
        foreach (var (rt, cg) in btnAnims)
        {
            StartCoroutine(FadeMove(cg, rt, 15f, 0.25f));
            yield return new WaitForSecondsRealtime(0.09f);
        }
    }

    IEnumerator FadeMove(CanvasGroup cg, RectTransform rt, float moveY, float duration)
    {
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(0, moveY);
        float   t        = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            cg.alpha = p;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            yield return null;
        }
        cg.alpha = 1f;
        rt.anchoredPosition = endPos;
    }

    // ─── UI Builder ──────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("MenuCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Arkaplan
        var bgGO = Stretch(canvasGO.transform, "BG");
        bgGO.AddComponent<Image>().color = BgColor;
        BuildScanLines(bgGO.transform);

        // ── Ana Panel ──
        mainPanel = new GameObject("MainPanel");
        mainPanel.transform.SetParent(canvasGO.transform, false);
        Anchor(mainPanel, new Vector2(0.5f, 0.5f), new Vector2(400, 460));

        // Başlık container'ı (glow + ana birlikte)
        var titleContainer = new GameObject("TitleContainer");
        titleContainer.transform.SetParent(mainPanel.transform, false);
        titleRT = titleContainer.AddComponent<RectTransform>();
        titleRT.anchoredPosition = new Vector2(0, 155);
        titleRT.sizeDelta        = new Vector2(800, 120);
        titleCG = titleContainer.AddComponent<CanvasGroup>();

        // Glow katmanı (arkada, aynı pozisyon, daha büyük + yarı saydam)
        AddTMP(titleContainer.transform, "BLOODRUSH", 78f, Vector2.zero, NeonRedGlow);
        // Ana başlık
        AddTMP(titleContainer.transform, "BLOODRUSH", 70f, Vector2.zero, NeonRed);

        // Butonlar
        AddMenuButton(mainPanel.transform, "BAŞLAT",  new Vector2(0,  30),  StartGame);
        AddMenuButton(mainPanel.transform, "AYARLAR", new Vector2(0, -50), OpenSettings);
        AddMenuButton(mainPanel.transform, "ÇIKIŞ",   new Vector2(0, -130), ExitGame);

        // ── Ayarlar Paneli ──
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(canvasGO.transform, false);
        Anchor(settingsPanel, new Vector2(0.5f, 0.5f), new Vector2(420, 420));

        var settBG = Stretch(settingsPanel.transform, "BG");
        settBG.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 0.97f);
        // Kırmızı üst çizgi
        var topLine = new GameObject("TopLine");
        topLine.transform.SetParent(settingsPanel.transform, false);
        var tlRT = topLine.AddComponent<RectTransform>();
        tlRT.anchorMin = new Vector2(0, 1);
        tlRT.anchorMax = new Vector2(1, 1);
        tlRT.pivot     = new Vector2(0.5f, 1);
        tlRT.sizeDelta = new Vector2(0, 3);
        topLine.AddComponent<Image>().color = NeonRed;

        MakeSettingsLabel(settingsPanel.transform, "AYARLAR", 34f, new Vector2(0, 155));

        float savedVol  = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedSens = PlayerPrefs.GetFloat("Sensitivity", 2f);

        volumeLabel = MakeSettingsLabel(settingsPanel.transform, $"SES   {savedVol:0.0}", 16f, new Vector2(0, 85));
        volumeSlider = MakeSlider(settingsPanel.transform, new Vector2(0, 58), 0f, 1f, savedVol, OnVolumeChanged);

        sensLabel = MakeSettingsLabel(settingsPanel.transform, $"HASSASİYET   {savedSens:0.0}", 16f, new Vector2(0, 10));
        sensSlider = MakeSlider(settingsPanel.transform, new Vector2(0, -18), 0.5f, 10f, savedSens, OnSensChanged);

        AddMenuButton(settingsPanel.transform, "GERİ", new Vector2(0, -110), CloseSettings, false);

        settingsPanel.SetActive(false);
    }

    // ─── Yardımcılar ─────────────────────────────────────────────────

    void BuildScanLines(Transform parent)
    {
        for (int i = 0; i < 160; i++)
        {
            var go = new GameObject("SL");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(0, i * 8);
            rt.offsetMax = new Vector2(0, i * 8 + 2);
            go.AddComponent<Image>().color = ScanLine;
        }
    }

    void AddMenuButton(Transform parent, string label, Vector2 pos,
                       UnityEngine.Events.UnityAction action, bool animated = true)
    {
        float w = 300f, h = 52f;

        // Kırmızı border
        var border = new GameObject("Btn_" + label);
        border.transform.SetParent(parent, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchoredPosition = pos;
        brt.sizeDelta        = new Vector2(w, h);
        border.AddComponent<Image>().color = NeonRed;

        // Koyu iç zemin (2px inset)
        var bgGO  = Stretch(border.transform, "BG", new Vector2(2, 2), new Vector2(-2, -2));
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BtnBg;

        // Hover dolgu
        var fillGO  = Stretch(border.transform, "Fill", new Vector2(2, 2), new Vector2(-2, -2));
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(NeonRedFill.r, NeonRedFill.g, NeonRedFill.b, 0f);

        // Yazı
        var textGO = Stretch(border.transform, "Text");
        var tmp    = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text             = label;
        tmp.fontSize         = 20f;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Color.white;
        tmp.characterSpacing = 4f;

        // Button
        var btn = border.AddComponent<Button>();
        btn.transition    = Selectable.Transition.None;
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(action);

        // Hover efekti
        var hover = border.AddComponent<ButtonHoverEffect>();
        hover.fillImage = fillImg;
        hover.fillColor = NeonRedFill;

        // Animasyon grubu
        var cg = border.AddComponent<CanvasGroup>();
        if (animated) btnAnims.Add((brt, cg));
    }

    void AddTMP(Transform parent, string text, float size, Vector2 offset, Color color)
    {
        var go = new GameObject("TMP");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta        = new Vector2(800, 120);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = size;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = color;
        tmp.characterSpacing = 10f;
        tmp.enableWordWrapping = false;
    }

    TextMeshProUGUI MakeSettingsLabel(Transform parent, string text, float size, Vector2 pos)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(380, 36);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = size;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Color.white;
        tmp.characterSpacing = 3f;
        return tmp;
    }

    Slider MakeSlider(Transform parent, Vector2 pos, float min, float max, float value,
                      UnityEngine.Events.UnityAction<float> onChange)
    {
        var go = new GameObject("Slider");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(340, 22);

        var slider       = go.AddComponent<Slider>();
        slider.minValue  = min;
        slider.maxValue  = max;
        slider.value     = value;

        Stretch(go.transform, "BG").AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        var fillArea = Stretch(go.transform, "FillArea", new Vector2(4, 0), new Vector2(-4, 0));
        var fill     = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = NeonRed;
        slider.fillRect = fillRT;

        var handleArea = Stretch(go.transform, "HandleArea", new Vector2(8, 0), new Vector2(-8, 0));
        var handle     = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRT   = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(14, 26);
        var handleImg  = handle.AddComponent<Image>();
        handleImg.color    = Color.white;
        slider.handleRect  = handleRT;
        slider.targetGraphic = handleImg;

        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    // ─── Rect helpers ────────────────────────────────────────────────

    static GameObject Stretch(Transform parent, string name,
                               Vector2 minOff = default, Vector2 maxOff = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = minOff;
        rt.offsetMax = maxOff;
        return go;
    }

    static void Anchor(GameObject go, Vector2 anchorCenter, Vector2 size)
    {
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorCenter;
        rt.anchorMax        = anchorCenter;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;
    }
}
}
